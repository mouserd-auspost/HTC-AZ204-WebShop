using System.Threading.Tasks;

public interface IBlobImageService
{
    Task<string> GetDisplayImageUrlAsync(string? blobName);
}
