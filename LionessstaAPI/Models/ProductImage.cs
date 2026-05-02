namespace LionessstaAPI.Models
{
    public class ProductImage
    {
        public int Id { get; set; }
        public string Label { get; set; }
        public string Category { get; set; }
        public string ImageUrl { get; set; }  // Azure Blob URL
        public DateTime CreatedAt { get; set; }
    }
}
