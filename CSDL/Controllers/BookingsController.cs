using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nhom1.DTOs;
using Nhom1.Models;
using Nhom1.Services;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingsController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var booking = await _bookingService.CreateBookingAsync(createDto, userId);
            if (booking == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to create booking. Homestay may not be available."));

            return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, 
                ApiResponse<BookingDto>.SuccessResponse(booking, "Booking created successfully"));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBooking(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var booking = await _bookingService.GetBookingByIdAsync(id, userId);
            if (booking == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Booking not found"));

            return Ok(ApiResponse<BookingDto>.SuccessResponse(booking));
        }

        [HttpGet("my-bookings")]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var bookings = await _bookingService.GetUserBookingsAsync(userId);
            return Ok(ApiResponse<List<BookingDto>>.SuccessResponse(bookings));
        }

        [HttpGet("host-bookings")]
        public async Task<IActionResult> GetHostBookings()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var bookings = await _bookingService.GetHostBookingsAsync(userId);
            return Ok(ApiResponse<List<BookingDto>>.SuccessResponse(bookings));
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusDto statusDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _bookingService.UpdateBookingStatusAsync(id, statusDto.Status, userId);
            if (!success)
                return NotFound(ApiResponse<object>.ErrorResponse("Booking not found or you don't have permission"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Booking status updated"));
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _bookingService.CancelBookingAsync(id, userId);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to cancel booking"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Booking cancelled successfully"));
        }

        [HttpPost("{id}/review")]
        public async Task<IActionResult> CreateReview(int id, [FromBody] CreateReviewDto reviewDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _bookingService.CreateReviewAsync(id, reviewDto, userId);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to create review. Booking may not be completed or already reviewed."));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Review created successfully"));
        }

        [AllowAnonymous]
        [HttpPost("check-availability")]
        public async Task<IActionResult> CheckAvailability([FromBody] BookingAvailabilityDto availabilityDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var result = await _bookingService.CheckAvailabilityAsync(availabilityDto);
            return Ok(ApiResponse<AvailabilityResponseDto>.SuccessResponse(result));
        }

        [AllowAnonymous]
        [HttpGet("homestays/{homestayId}/booked-dates")]
        public async Task<IActionResult> GetBookedDates(int homestayId)
        {
            var bookedDates = await _bookingService.GetBookedDatesAsync(homestayId);
            var dateStrings = bookedDates.Select(d => d.ToString("yyyy-MM-dd")).ToList();
            return Ok(ApiResponse<List<string>>.SuccessResponse(dateStrings));
        }

        [AllowAnonymous]
        [HttpPost("calculate-amount")]
        public async Task<IActionResult> CalculateAmount([FromBody] CalculateAmountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid data"));

            var result = await _bookingService.CalculateBookingAmountAsync(
                dto.HomestayId, 
                dto.CheckIn, 
                dto.CheckOut, 
                dto.PromotionCode
            );
            
            return Ok(ApiResponse<AmountCalculationDto>.SuccessResponse(result));
        }
    }
}
