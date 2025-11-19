using System;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Azure.Storage;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class BlobImageService : IBlobImageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _comingSoonBlobName = "commingsoon.jpg"; // corrected spelling
    private readonly StorageSharedKeyCredential _sharedKeyCredential;
    private readonly string _accountName;
    private readonly string _containerName;
    private readonly int _sasExpiryMinutes;
    private readonly ILogger<BlobImageService> _logger;

    public BlobImageService(IConfiguration configuration, ILogger<BlobImageService> logger)
    {
        _logger = logger;
        var conn = configuration["AzureStorage:ConnectionString"] ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("Azure Storage connection string not configured. Set AzureStorage:ConnectionString or AZURE_STORAGE_CONNECTION_STRING env var.");
        }
        _containerName = configuration["AzureStorage:ContainerName"] ?? throw new InvalidOperationException("AzureStorage:ContainerName is not configured.");
        _sasExpiryMinutes = int.TryParse(configuration["AzureStorage:SasExpiryMinutes"], out var m) ? m : 60;

        // Parse connection string for account and key.
        // Connection string segments: name=value;...
        var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries);
        string? accountKey = null;
        string? accountName = null;
        foreach (var p in parts)
        {
            var kv = p.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Equals("AccountName", StringComparison.OrdinalIgnoreCase)) accountName = kv[1];
            if (kv[0].Equals("AccountKey", StringComparison.OrdinalIgnoreCase)) accountKey = kv[1];
        }
        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(accountKey))
        {
            throw new FormatException("Connection string missing AccountName or AccountKey needed for SAS generation.");
        }
        _accountName = accountName;
        _sharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);
        _containerClient = new BlobContainerClient(conn, _containerName);
    }

    public async Task<string> GetDisplayImageUrlAsync(string? blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return await GetComingSoonUrlAsync();
        }

        var blobClient = _containerClient.GetBlobClient(blobName);
        BlobProperties? props = null;
        try
        {
            props = (await blobClient.GetPropertiesAsync()).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Blob {BlobName} not found, using coming soon image.", blobName);
            return await GetComingSoonUrlAsync();
        }
        catch (RequestFailedException ex)
        {
            // Authorization / other errors: log and continue with original image (no ReleaseDate gating)
            _logger.LogWarning(ex, "Failed to get properties for blob {BlobName} (Status {Status}). Serving original image.", blobName, ex.Status);
            return GenerateBlobSasUri(blobClient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving properties for blob {BlobName}. Serving original image.", blobName);
            return GenerateBlobSasUri(blobClient);
        }

        // If we have properties, evaluate ReleaseDate metadata.
        if (props != null)
        {
            string? releaseDateValue = null;
            if (props.Metadata.TryGetValue("ReleaseDate", out var rd) ||
                props.Metadata.TryGetValue("releasedate", out rd) ||
                props.Metadata.TryGetValue("releaseDate", out rd))
            {
                releaseDateValue = rd;
            }

            if (!string.IsNullOrWhiteSpace(releaseDateValue))
            {
                DateTime releaseDate;
                var formats = new[] {
                    "yyyy-MM-dd",
                    "yyyy-MM-ddTHH:mm:ssZ",
                    "yyyy-MM-ddTHH:mm:ssK",
                    "yyyy-MM-ddTHH:mm:ss.fffZ",
                    "yyyy-MM-ddTHH:mm:ss.fffK"
                };
                bool parsed = DateTime.TryParseExact(
                    releaseDateValue,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out releaseDate);
                if (!parsed)
                {
                    parsed = DateTime.TryParse(releaseDateValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out releaseDate);
                }
                if (parsed && releaseDate > DateTime.UtcNow)
                {
                    _logger.LogDebug("Blob {BlobName} gated until {ReleaseDate:O}. Returning coming soon image.", blobName, releaseDate);
                    return await GetComingSoonUrlAsync();
                }
            }
        }

        return GenerateBlobSasUri(blobClient);
    }

    private async Task<string> GetComingSoonUrlAsync()
    {
        var comingSoonClient = _containerClient.GetBlobClient(_comingSoonBlobName);
        try { await _containerClient.ExistsAsync(); } catch { }
        return GenerateBlobSasUri(comingSoonClient);
    }

    private string GenerateBlobSasUri(BlobClient blobClient)
    {
        // Build SAS granting read access.
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobClient.Name,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // clock skew allowance
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_sasExpiryMinutes)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        var sas = sasBuilder.ToSasQueryParameters(_sharedKeyCredential).ToString();
        var uriBuilder = new UriBuilder(blobClient.Uri)
        {
            Query = sas
        };
        return uriBuilder.Uri.ToString();
    }
}
