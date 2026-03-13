using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;
using Nhom1.Services;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [Authorize(Roles = "Host,Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class HostController : ControllerBase
    {
        private readonly IHomestayService _homestayService;
        private readonly IBookingService _bookingService;
        private readonly ApplicationDbContext _context;

        public HostController(IHomestayService homestayService, IBookingService bookingService, ApplicationDbContext context)
        {
            _homestayService = homestayService;
            _bookingService = bookingService;
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetHostStats()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var totalHomestays = await _homestayService.GetHomestaysCountByHostAsync(userId);
                var totalBookings = await _bookingService.GetBookingsCountByHostAsync(userId);

                var stats = new
                {
                    totalHomestays,
                    totalBookings
                };

                return Ok(ApiResponse<object>.SuccessResponse(stats));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        [HttpGet("revenue")]
        public async Task<IActionResult> GetHostRevenue()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                // Get total revenue
                var totalRevenue = await _bookingService.GetTotalRevenueByHostAsync(userId);

                // Get current month revenue
                var currentMonthRevenue = await _bookingService.GetCurrentMonthRevenueByHostAsync(userId);

                // Get completed bookings count
                var completedBookingsCount = await _bookingService.GetCompletedBookingsCountByHostAsync(userId);

                // Get total bookings count for completion rate
                var totalBookingsCount = await _bookingService.GetBookingsCountByHostAsync(userId);
                var completionRate = totalBookingsCount > 0
                    ? (double)completedBookingsCount / totalBookingsCount * 100
                    : 0;

                // Get monthly revenue for last 6 months
                var monthlyRevenue = await _bookingService.GetMonthlyRevenueByHostAsync(userId, 6);

                var revenueData = new
                {
                    totalRevenue,
                    currentMonthRevenue,
                    completedBookingsCount,
                    completionRate = Math.Round(completionRate, 1),
                    monthlyRevenue
                };

                return Ok(ApiResponse<object>.SuccessResponse(revenueData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        [HttpGet("reviews")]
        public async Task<IActionResult> GetHostReviews()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var reviews = await _context.Bookings
                    .Where(b => b.Homestay.HostId == userId &&
                               b.Status == BookingStatus.Completed &&
                               !string.IsNullOrEmpty(b.ReviewComment))
                    .Select(b => new
                    {
                        Id = b.Id,
                        Rating = b.ReviewRating,
                        Comment = b.ReviewComment,
                        CreatedAt = b.ReviewCreatedAt,
                        HostReply = b.HostReply,
                        HomestayName = b.Homestay.Name,
                        GuestName = b.User.FullName,
                        HomestayId = b.HomestayId
                    })
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                var stats = new
                {
                    totalReviews = reviews.Count,
                    averageRating = reviews.Any() ? reviews.Average(r => r.Rating ?? 0) : 0.0,
                    fiveStarCount = reviews.Count(r => (r.Rating ?? 0) == 5),
                    otherStarsCount = reviews.Count(r => (r.Rating ?? 0) < 5 && (r.Rating ?? 0) > 0)
                };

                var result = new
                {
                    reviews,
                    stats
                };

                return Ok(ApiResponse<object>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }
    }
}