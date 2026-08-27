using System.ComponentModel.DataAnnotations;

namespace LionessstaAPI.DTOs
{
    // All fields nullable -- send only what you want to change
    public class CategoryUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(100)]
        public string? Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
    }
}
