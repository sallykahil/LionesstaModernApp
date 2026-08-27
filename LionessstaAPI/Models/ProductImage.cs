namespace LionessstaAPI.Models
{
    public class ProductImage
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public string ImageUrl { get; set; } = string.Empty;  // Azure Blob URL
        public DateTime CreatedAt { get; set; }
    }
}
