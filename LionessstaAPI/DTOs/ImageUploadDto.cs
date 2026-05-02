using System.ComponentModel.DataAnnotations;

namespace LionessstaAPI.DTOs
{
    // Received from the admin panel when uploading a new image
    // Sent as multipart/form-data (because it contains a file)
    public class ImageUploadDto
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Label { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty; // "bags" | "accessories" | "home"
    }
}