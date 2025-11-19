namespace Contoso.Api.Data
{
    public class ProductImageDto
    {
        public string? ImageUrl { get; set; }

        public byte[]? Image { get; set; }
        
        // Optional metadata to persist on the uploaded blob (e.g., ReleaseDate)
        public Dictionary<string, string>? Metadata { get; set; }
    }
}