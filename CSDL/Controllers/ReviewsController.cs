using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nhom1.DTOs;
using Nhom1.Services;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [AllowAnonymous]
        [HttpGet("homestay/{homestayId}")]
        public async Task<IActionResult> GetHomestayReviews(int homestayId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var reviews = await _reviewService.GetHomestayReviewsAsync(homestayId, page, pageSize);
            return Ok(ApiResponse<List<ReviewDto>>.SuccessResponse(reviews));
        }

        [Authorize]
        [HttpPost("{bookingId}")]
        public async Task<IActionResult> CreateReview(int bookingId, [FromBody] CreateReviewDto reviewDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _reviewService.CreateReviewAsync(bookingId, userId, reviewDto.Rating, reviewDto.Comment);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to create review. Booking may not exist or not completed."));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Review created successfully"));
        }
        [Authorize]
        [HttpPost("{bookingId}/response")]
        public async Task<IActionResult> AddHostResponse(int bookingId, [FromBody] CreateHostResponseDto responseDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _reviewService.AddHostResponseAsync(bookingId, userId, responseDto.Response);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to add response. You may not be the host or review doesn't exist."));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Host response added successfully"));
        }

        [Authorize]
        [HttpPost("{bookingId}/reply")]
        public async Task<IActionResult> ReplyToReview(int bookingId, [FromBody] ReplyToReviewDto replyDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _reviewService.AddHostResponseAsync(bookingId, userId, replyDto.Reply);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to reply to review. You may not be the host or review doesn't exist."));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Reply sent successfully"));
        }
    }
}
