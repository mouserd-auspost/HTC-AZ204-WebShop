using System.Collections.Generic;
using System.Threading.Tasks;

public interface IBlobImageService
{
    Task<string> GetDisplayImageUrlAsync(string? blobName);

    // Upload a blob with the provided name and content. Returns the saved blob name.
    Task<string> UploadImageAsync(string blobName, byte[] content, IDictionary<string, string>? metadata = null);
}
