using System.ComponentModel.DataAnnotations;

namespace LionessstaAPI.DTOs
{
    // Sent from admin panel when editing an existing image
    // Sent as JSON (not form-data — no file here)
    // Both fields are nullable — send only what you want to change
    public class ImageUpdateDto
    {
        [MaxLength(100)]
        public string? Label { get; set; }    // null = don't change

        [MaxLength(50)]
        public string? Category { get; set; } // null = don't change
    }
}