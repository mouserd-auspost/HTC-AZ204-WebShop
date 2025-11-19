public class ProductDto
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string Category { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    public byte[] Image { get; set; } 

    // Resolved blob URI taking ReleaseDate metadata into account.
    public string? DisplayImageUrl { get; set; }


}