namespace LionessstaAPI.DTOs
{
    // Sent back to the frontend in ALL GET responses and after upload
    // This is what the storefront and admin panel receive to build product cards
    public class ImageResponseDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategorySlug { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty; // Azure Blob public URL → goes into <img src="..."/>
        public DateTime CreatedAt { get; set; }
    }
}
