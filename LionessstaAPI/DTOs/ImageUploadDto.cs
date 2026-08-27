using System.ComponentModel.DataAnnotations;

namespace LionessstaAPI.DTOs
{
    // Received from the admin panel when uploading a new product
    // Sent as multipart/form-data (because it contains a file)
    public class ImageUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Label { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [Range(0, 100000)]
        public decimal Price { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}
