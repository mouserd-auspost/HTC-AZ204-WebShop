using System.ComponentModel.DataAnnotations;

namespace Contoso.Api.Data
{
    public class ProductDto
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public string? Category { get; set; }

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public byte[]? Image { get; set; }

        public DateTime? ReleaseDate { get; set; }
    }
}