namespace LionessstaAPI.DTOs
{
    // Sent back to the frontend in ALL GET responses and after upload
    // This is what your gallery JS receives and uses to build image cards
    public class ImageResponseDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty; // Azure Blob public URL → goes into <img src="..."/>
        public DateTime CreatedAt { get; set; }
    }
}