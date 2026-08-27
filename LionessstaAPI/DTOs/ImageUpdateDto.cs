using System.ComponentModel.DataAnnotations;

namespace LionessstaAPI.DTOs
{
    // Sent from admin panel when editing an existing product
    // Sent as JSON (not form-data — no file here)
    // All fields are nullable — send only what you want to change
    public class ImageUpdateDto
    {
        [MaxLength(100)]
        public string? Label { get; set; }    // null = don't change

        public int? CategoryId { get; set; }  // null = don't change

        [Range(0, 100000)]
        public decimal? Price { get; set; }   // null = don't change

        [MaxLength(1000)]
        public string? Description { get; set; } // null = don't change
    }
}
