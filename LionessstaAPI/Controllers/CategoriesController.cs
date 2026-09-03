using LionessstaAPI.Data;
using LionessstaAPI.DTOs;
using LionessstaAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LionessstaAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<CategoriesController> _logger;
        private readonly IMemoryCache _cache;

        // Category renames/deletes affect the CategoryName/Slug embedded in
        // every cached image DTO too, so both cache keys are cleared together.
        private const string CategoriesCacheKey = "categories:all";
        private const string ImagesCacheKey = "images:all";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        public CategoriesController(AppDbContext db, ILogger<CategoriesController> logger, IMemoryCache cache)
        {
            _db = db;
            _logger = logger;
            _cache = cache;
        }

        private static CategoryResponseDto ToDto(Category c, int productCount) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            ProductCount = productCount,
            CreatedAt = c.CreatedAt
        };

        // GET /api/categories
        // Public — called by the storefront and admin panel on page load
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var result = await _cache.GetOrCreateAsync(CategoriesCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;

                var categories = await _db.Categories
                    .OrderBy(c => c.Name)
                    .Select(c => new { Category = c, ProductCount = c.Products.Count })
                    .ToListAsync();

                return categories.Select(x => ToDto(x.Category, x.ProductCount)).ToList();
            });

            return Ok(result);
        }

        // GET /api/categories/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _db.Categories
                .Select(c => new { Category = c, ProductCount = c.Products.Count })
                .FirstOrDefaultAsync(x => x.Category.Id == id);

            if (category == null)
                return NotFound(new { message = $"Category {id} not found." });

            return Ok(ToDto(category.Category, category.ProductCount));
        }

        // POST /api/categories
        // Admin only
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CategoryCreateDto dto)
        {
            var slug = dto.Slug.Trim().ToLower();

            var slugTaken = await _db.Categories.AnyAsync(c => c.Slug.ToLower() == slug);
            if (slugTaken)
                return BadRequest(new { message = $"A category with slug '{slug}' already exists." });

            var category = new Category
            {
                Name = dto.Name.Trim(),
                Slug = slug,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow
            };

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();
            _cache.Remove(CategoriesCacheKey);

            return CreatedAtAction(nameof(GetById), new { id = category.Id }, ToDto(category, 0));
        }

        // PUT /api/categories/5
        // Admin only — send only the fields you want to change
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryUpdateDto dto)
        {
            var category = await _db.Categories.FindAsync(id);

            if (category == null)
                return NotFound(new { message = $"Category {id} not found." });

            if (!string.IsNullOrEmpty(dto.Slug))
            {
                var newSlug = dto.Slug.Trim().ToLower();
                var slugTaken = await _db.Categories.AnyAsync(c => c.Id != id && c.Slug.ToLower() == newSlug);
                if (slugTaken)
                    return BadRequest(new { message = $"A category with slug '{newSlug}' already exists." });

                category.Slug = newSlug;
            }

            if (!string.IsNullOrEmpty(dto.Name))
                category.Name = dto.Name.Trim();

            if (dto.Description != null)
                category.Description = dto.Description;

            await _db.SaveChangesAsync();
            _cache.Remove(CategoriesCacheKey);
            _cache.Remove(ImagesCacheKey); // embedded CategoryName/Slug on cached images may now be stale

            return Ok(new { message = "Updated successfully." });
        }

        // DELETE /api/categories/5
        // Admin only — blocked if the category still has products (see AppDbContext delete behavior)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _db.Categories.FindAsync(id);

            if (category == null)
                return NotFound(new { message = $"Category {id} not found." });

            var productCount = await _db.ProductImages.CountAsync(p => p.CategoryId == id);
            if (productCount > 0)
                return BadRequest(new { message = $"Cannot delete '{category.Name}' -- it still has {productCount} product(s). Reassign or delete them first." });

            try
            {
                _db.Categories.Remove(category);
                await _db.SaveChangesAsync();
                _cache.Remove(CategoriesCacheKey);

                return Ok(new { message = "Deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete failed for category {Id}", id);
                return StatusCode(500, new { message = "Delete failed. Please try again." });
            }
        }
    }
}
