using LionessstaAPI.Data;
using LionessstaAPI.DTOs;
using LionessstaAPI.Models;
using LionessstaAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LionessstaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IBlobService _blobService;

        public ImagesController(AppDbContext db, IBlobService blobService)
        {
            _db = db;
            _blobService = blobService;
        }

        // GET /api/images
        // GET /api/images?category=bags
        // Public — called by your frontend gallery on page load
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] string? category)
        {
            var query = _db.ProductImages.AsQueryable();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(img => img.Category.ToLower() == category.ToLower());

            var images = await query
                .OrderByDescending(img => img.CreatedAt)
                .Select(img => new ImageResponseDto
                {
                    Id = img.Id,
                    Label = img.Label,
                    Category = img.Category,
                    ImageUrl = img.ImageUrl,
                    CreatedAt = img.CreatedAt
                })
                .ToListAsync();

            return Ok(images);
        }

        // GET /api/images/5
        // Public — returns one image by ID
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var image = await _db.ProductImages.FindAsync(id);

            if (image == null)
                return NotFound(new { message = $"Image {id} not found." });

            return Ok(new ImageResponseDto
            {
                Id = image.Id,
                Label = image.Label,
                Category = image.Category,
                ImageUrl = image.ImageUrl,
                CreatedAt = image.CreatedAt
            });
        }

        // POST /api/images/upload
        // Admin only — receives file + label + category
        // Resizes image → uploads to Azure → saves URL in DB
        [HttpPost("upload")]
        //[Authorize]
        public async Task<IActionResult> Upload([FromForm] ImageUploadDto dto)
        {
            if (dto.File == null || dto.File.Length == 0)
                return BadRequest(new { message = "No file provided." });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(dto.File.ContentType.ToLower()))
                return BadRequest(new { message = "Only JPEG, PNG and WebP allowed." });

            if (dto.File.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "Max file size is 10MB." });

            try
            {
                using var stream = dto.File.OpenReadStream();

                // BlobService handles resize + Azure upload, returns public URL
                var imageUrl = await _blobService.UploadImageAsync(
                    stream,
                    dto.File.FileName,
                    dto.File.ContentType
                );

                var image = new ProductImage
                {
                    Label = dto.Label,
                    Category = dto.Category.ToLower(),
                    ImageUrl = imageUrl,
                    CreatedAt = DateTime.UtcNow
                };

                _db.ProductImages.Add(image);
                await _db.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = image.Id }, new ImageResponseDto
                {
                    Id = image.Id,
                    Label = image.Label,
                    Category = image.Category,
                    ImageUrl = image.ImageUrl,
                    CreatedAt = image.CreatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = ex.Message,           // real error
                    inner = ex.InnerException?.Message,  // inner error if any
                    stack = ex.StackTrace         // exact line that failed
                });
            }
        }

        // PUT /api/images/5
        // Admin only — edit label and/or category, does NOT re-upload the image
        [HttpPut("{id}")]
        //[Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] ImageUpdateDto dto)
        {
            var image = await _db.ProductImages.FindAsync(id);

            if (image == null)
                return NotFound(new { message = $"Image {id} not found." });

            if (!string.IsNullOrEmpty(dto.Label))
                image.Label = dto.Label;

            if (!string.IsNullOrEmpty(dto.Category))
                image.Category = dto.Category.ToLower();

            await _db.SaveChangesAsync();

            return Ok(new { message = "Updated successfully." });
        }

        // DELETE /api/images/5
        // Admin only — deletes from Azure Blob AND from the database
        [HttpDelete("{id}")]
        //[Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var image = await _db.ProductImages.FindAsync(id);

            if (image == null)
                return NotFound(new { message = $"Image {id} not found." });

            try
            {
                await _blobService.DeleteImageAsync(image.ImageUrl);

                _db.ProductImages.Remove(image);
                await _db.SaveChangesAsync();

                return Ok(new { message = "Deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete error: {ex.Message}");
                return StatusCode(500, new { message = "Delete failed. Please try again." });
            }
        }
    }
}
