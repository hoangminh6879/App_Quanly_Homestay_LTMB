using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UploadController> _logger;

        public UploadController(
            ApplicationDbContext context, 
            IWebHostEnvironment environment,
            ILogger<UploadController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Upload multiple images for a homestay
        /// </summary>
        [Authorize]
        [HttpPost("homestay/{homestayId}/images")]
        public async Task<IActionResult> UploadHomestayImages(int homestayId, [FromForm] List<IFormFile> images)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Verify homestay ownership
            var homestay = await _context.Homestays
                .FirstOrDefaultAsync(h => h.Id == homestayId && h.HostId == userId);

            if (homestay == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found or you don't have permission"));

            if (images == null || !images.Any())
                return BadRequest(ApiResponse<object>.ErrorResponse("No images provided"));

            if (images.Count > 10)
                return BadRequest(ApiResponse<object>.ErrorResponse("Maximum 10 images allowed"));

            var uploadedImages = new List<HomestayImageDto>();
            var uploadPath = Path.Combine(_environment.WebRootPath, "images", "homestays", homestayId.ToString());

            // Create directory if not exists
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            foreach (var image in images)
            {
                // Validate file
                if (image.Length == 0)
                    continue;

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    _logger.LogWarning($"Invalid file extension: {extension}");
                    continue;
                }

                if (image.Length > 5 * 1024 * 1024) // 5MB
                {
                    _logger.LogWarning($"File too large: {image.Length} bytes");
                    continue;
                }

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                // Get current max order
                var maxOrder = await _context.HomestayImages
                    .Where(i => i.HomestayId == homestayId)
                    .MaxAsync(i => (int?)i.Order) ?? -1;

                // Add to database
                var imageUrl = $"/images/homestays/{homestayId}/{fileName}";
                var homestayImage = new HomestayImage
                {
                    HomestayId = homestayId,
                    ImageUrl = imageUrl,
                    IsPrimary = maxOrder < 0, // First image is primary
                    Order = maxOrder + 1,
                    CreatedAt = DateTime.UtcNow
                };

                _context.HomestayImages.Add(homestayImage);
                await _context.SaveChangesAsync();

                uploadedImages.Add(new HomestayImageDto
                {
                    Id = homestayImage.Id,
                    ImageUrl = imageUrl,
                    IsPrimary = homestayImage.IsPrimary,
                    DisplayOrder = homestayImage.Order
                });
            }

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                uploadedCount = uploadedImages.Count,
                images = uploadedImages
            }, $"Successfully uploaded {uploadedImages.Count} images"));
        }

        /// <summary>
        /// Set primary image for homestay
        /// </summary>
        [Authorize]
        [HttpPut("homestay/{homestayId}/images/{imageId}/set-primary")]
        public async Task<IActionResult> SetPrimaryImage(int homestayId, int imageId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Verify ownership
            var homestay = await _context.Homestays
                .FirstOrDefaultAsync(h => h.Id == homestayId && h.HostId == userId);

            if (homestay == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found"));

            // Remove primary from all images
            var allImages = await _context.HomestayImages
                .Where(i => i.HomestayId == homestayId)
                .ToListAsync();

            foreach (var img in allImages)
            {
                img.IsPrimary = img.Id == imageId;
            }

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Primary image updated"));
        }

        /// <summary>
        /// Delete homestay image
        /// </summary>
        [Authorize]
        [HttpDelete("homestay/{homestayId}/images/{imageId}")]
        public async Task<IActionResult> DeleteHomestayImage(int homestayId, int imageId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Verify ownership
            var homestay = await _context.Homestays
                .FirstOrDefaultAsync(h => h.Id == homestayId && h.HostId == userId);

            if (homestay == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found"));

            var image = await _context.HomestayImages
                .FirstOrDefaultAsync(i => i.Id == imageId && i.HomestayId == homestayId);

            if (image == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Image not found"));

            // Delete physical file
            try
            {
                var filePath = Path.Combine(_environment.WebRootPath, image.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete physical file");
            }

            // Delete from database
            _context.HomestayImages.Remove(image);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Image deleted successfully"));
        }

        /// <summary>
        /// Reorder homestay images
        /// </summary>
        [Authorize]
        [HttpPut("homestay/{homestayId}/images/reorder")]
        public async Task<IActionResult> ReorderImages(int homestayId, [FromBody] List<ImageOrderDto> imageOrders)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Verify ownership
            var homestay = await _context.Homestays
                .FirstOrDefaultAsync(h => h.Id == homestayId && h.HostId == userId);

            if (homestay == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found"));

            foreach (var orderDto in imageOrders)
            {
                var image = await _context.HomestayImages
                    .FirstOrDefaultAsync(i => i.Id == orderDto.ImageId && i.HomestayId == homestayId);

                if (image != null)
                {
                    image.Order = orderDto.Order;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Images reordered successfully"));
        }

        /// <summary>
        /// Upload user avatar (file upload)
        /// </summary>
        [Authorize]
        [HttpPost("avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (avatar == null || avatar.Length == 0)
                return BadRequest(ApiResponse<object>.ErrorResponse("No file provided"));

            // Validate
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(avatar.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid file type. Only jpg, jpeg, png, gif allowed"));

            if (avatar.Length > 2 * 1024 * 1024) // 2MB
                return BadRequest(ApiResponse<object>.ErrorResponse("File too large. Maximum 2MB"));

            // Save file
            var uploadPath = Path.Combine(_environment.WebRootPath, "images", "users");
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            var fileName = $"{userId}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatar.CopyToAsync(stream);
            }

            // Update user
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                // Delete old avatar if exists
                if (!string.IsNullOrEmpty(user.ProfilePicture) && !user.ProfilePicture.Contains("default-avatar"))
                {
                    try
                    {
                        var oldPath = Path.Combine(_environment.WebRootPath, user.ProfilePicture.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }
                    catch { }
                }

                user.ProfilePicture = $"/images/users/{fileName}";
                await _context.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.SuccessResponse(new { avatarUrl = user?.ProfilePicture }, "Avatar uploaded successfully"));
        }
    }

    public class ImageOrderDto
    {
        public int ImageId { get; set; }
        public int Order { get; set; }
    }
}
