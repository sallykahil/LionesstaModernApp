namespace LionessstaAPI.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty; // url-safe key, e.g. "amigurumi"
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<ProductImage> Products { get; set; } = new List<ProductImage>();
    }
}
