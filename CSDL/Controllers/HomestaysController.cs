using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nhom1.DTOs;
using Nhom1.Services;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomestaysController : ControllerBase
    {
        private readonly IHomestayService _homestayService;

        public HomestaysController(IHomestayService homestayService)
        {
            _homestayService = homestayService;
        }

        [HttpGet]
        public async Task<IActionResult> GetHomestays([FromQuery] HomestaySearchDto searchDto)
        {
            var result = await _homestayService.GetHomestaysAsync(searchDto);
            return Ok(ApiResponse<PagedResponse<HomestayDto>>.SuccessResponse(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHomestay(int id)
        {
            var homestay = await _homestayService.GetHomestayByIdAsync(id);
            if (homestay == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found"));

            return Ok(ApiResponse<HomestayDto>.SuccessResponse(homestay));
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateHomestay([FromBody] CreateHomestayDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var homestay = await _homestayService.CreateHomestayAsync(createDto, userId);
            if (homestay == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to create homestay"));

            return CreatedAtAction(nameof(GetHomestay), new { id = homestay.Id }, 
                ApiResponse<HomestayDto>.SuccessResponse(homestay, "Homestay created successfully"));
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHomestay(int id, [FromBody] UpdateHomestayDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var homestay = await _homestayService.UpdateHomestayAsync(id, updateDto, userId);
            if (homestay == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found or you don't have permission"));

            return Ok(ApiResponse<HomestayDto>.SuccessResponse(homestay, "Homestay updated successfully"));
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHomestay(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _homestayService.DeleteHomestayAsync(id, userId);
            if (!success)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found or you don't have permission"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Homestay deleted successfully"));
        }

        [Authorize(Roles = "Host,Admin")]
        [HttpGet("my-homestays")]
        public async Task<IActionResult> GetMyHomestays()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var homestays = await _homestayService.GetUserHomestaysAsync(userId);
            return Ok(ApiResponse<List<HomestayDto>>.SuccessResponse(homestays));
        }

        [Authorize]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateHomestayStatus(int id, [FromBody] UpdateHomestayStatusDto statusDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _homestayService.UpdateHomestayStatusAsync(id, statusDto.IsActive, userId);
            if (!success)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found or you don't have permission"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Homestay status updated successfully"));
        }

        [Authorize]
        [HttpPost("{id}/images")]
        public async Task<IActionResult> UploadHomestayImages(int id, [FromForm] List<IFormFile> images)
        {
            if (images == null || !images.Any())
                return BadRequest(ApiResponse<object>.ErrorResponse("No images provided"));

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var imageUrls = await _homestayService.UploadHomestayImagesAsync(id, images, userId);
            if (imageUrls == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found or you don't have permission"));

            return Ok(ApiResponse<List<string>>.SuccessResponse(imageUrls, "Images uploaded successfully"));
        }

        [Authorize]
        [HttpPut("{id}/images/{imageId}/primary")]
        public async Task<IActionResult> SetPrimaryImage(int id, int imageId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _homestayService.SetPrimaryImageAsync(id, imageId, userId);
            if (!success)
                return NotFound(ApiResponse<object>.ErrorResponse("Image not found or you don't have permission"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Primary image set successfully"));
        }

        [Authorize]
        [HttpDelete("{id}/images/{imageId}")]
        public async Task<IActionResult> DeleteHomestayImage(int id, int imageId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _homestayService.DeleteHomestayImageAsync(id, imageId, userId);
            if (!success)
                return NotFound(ApiResponse<object>.ErrorResponse("Image not found or you don't have permission"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Image deleted successfully"));
        }
    }
}
