using LionessstaAPI.Data;
using LionessstaAPI.DTOs;
using LionessstaAPI.Models;
using LionessstaAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LionessstaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IBlobService _blobService;
        private readonly ILogger<ImagesController> _logger;
        private readonly IMemoryCache _cache;

        // Only the unfiltered list is cached -- it's the only shape the storefront
        // and admin panel ever actually request (both fetch everything once and
        // filter client-side), so filtered queries just bypass the cache.
        private const string ImagesCacheKey = "images:all";
        // Must match CategoriesController's key -- every product write here also
        // shifts a category's ProductCount, which is embedded in that cached list.
        private const string CategoriesCacheKey = "categories:all";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        public ImagesController(AppDbContext db, IBlobService blobService, ILogger<ImagesController> logger, IMemoryCache cache)
        {
            _db = db;
            _blobService = blobService;
            _logger = logger;
            _cache = cache;
        }

        private static ImageResponseDto ToDto(ProductImage img) => new()
        {
            Id = img.Id,
            Label = img.Label,
            Price = img.Price,
            Description = img.Description,
            CategoryId = img.CategoryId,
            CategoryName = img.Category?.Name ?? string.Empty,
            CategorySlug = img.Category?.Slug ?? string.Empty,
            ImageUrl = img.ImageUrl,
            CreatedAt = img.CreatedAt
        };

        // GET /api/images
        // GET /api/images?categorySlug=bags
        // GET /api/images?categoryId=3
        // Public — called by the storefront on page load
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] string? categorySlug, [FromQuery] int? categoryId)
        {
            var noFilter = !categoryId.HasValue && string.IsNullOrEmpty(categorySlug);

            if (noFilter)
            {
                var cached = await _cache.GetOrCreateAsync(ImagesCacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                    return await _db.ProductImages.Include(img => img.Category)
                        .OrderByDescending(img => img.CreatedAt)
                        .Select(img => ToDto(img))
                        .ToListAsync();
                });

                return Ok(cached);
            }

            var query = _db.ProductImages.Include(img => img.Category).AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(img => img.CategoryId == categoryId.Value);
            else
                query = query.Where(img => img.Category!.Slug.ToLower() == categorySlug!.ToLower());

            var images = await query
                .OrderByDescending(img => img.CreatedAt)
                .Select(img => ToDto(img))
                .ToListAsync();

            return Ok(images);
        }

        // GET /api/images/5
        // Public — returns one image by ID
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var image = await _db.ProductImages.Include(img => img.Category)
                .FirstOrDefaultAsync(img => img.Id == id);

            if (image == null)
                return NotFound(new { message = $"Image {id} not found." });

            return Ok(ToDto(image));
        }

        // POST /api/images/upload
        // Admin only — receives file + label + categoryId + price + description
        // Resizes image → uploads to Azure → saves URL in DB
        [HttpPost("upload")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Upload([FromForm] ImageUploadDto dto)
        {
            if (dto.File == null || dto.File.Length == 0)
                return BadRequest(new { message = "No file provided." });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(dto.File.ContentType.ToLower()))
                return BadRequest(new { message = "Only JPEG, PNG and WebP allowed." });

            if (dto.File.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "Max file size is 10MB." });

            var category = await _db.Categories.FindAsync(dto.CategoryId);
            if (category == null)
                return BadRequest(new { message = $"Category {dto.CategoryId} does not exist." });

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
                    CategoryId = dto.CategoryId,
                    Price = dto.Price,
                    Description = dto.Description,
                    ImageUrl = imageUrl,
                    CreatedAt = DateTime.UtcNow
                };

                _db.ProductImages.Add(image);
                await _db.SaveChangesAsync();
                _cache.Remove(ImagesCacheKey);
                _cache.Remove(CategoriesCacheKey); // this category's ProductCount just changed

                image.Category = category;

                return CreatedAtAction(nameof(GetById), new { id = image.Id }, ToDto(image));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed for file {FileName}", dto.File.FileName);
                return StatusCode(500, new { message = "Upload failed. Please try again." });
            }
        }

        // PUT /api/images/5
        // Admin only — edit label, category, price, and/or description, does NOT re-upload the image
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] ImageUpdateDto dto)
        {
            var image = await _db.ProductImages.FindAsync(id);

            if (image == null)
                return NotFound(new { message = $"Image {id} not found." });

            if (!string.IsNullOrEmpty(dto.Label))
                image.Label = dto.Label;

            var categoryChanged = false;
            if (dto.CategoryId.HasValue)
            {
                var categoryExists = await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId.Value);
                if (!categoryExists)
                    return BadRequest(new { message = $"Category {dto.CategoryId} does not exist." });

                categoryChanged = image.CategoryId != dto.CategoryId.Value;
                image.CategoryId = dto.CategoryId.Value;
            }

            if (dto.Price.HasValue)
                image.Price = dto.Price.Value;

            if (dto.Description != null)
                image.Description = dto.Description;

            await _db.SaveChangesAsync();
            _cache.Remove(ImagesCacheKey);
            if (categoryChanged) _cache.Remove(CategoriesCacheKey); // product counts shifted between categories

            return Ok(new { message = "Updated successfully." });
        }

        // DELETE /api/images/5
        // Admin only — deletes from Azure Blob AND from the database
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
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
                _cache.Remove(ImagesCacheKey);
                _cache.Remove(CategoriesCacheKey); // that category's ProductCount just dropped by one

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
