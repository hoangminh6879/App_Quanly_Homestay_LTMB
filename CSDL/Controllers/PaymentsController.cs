using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nhom1.DTOs;
using Nhom1.Models;
using Nhom1.Services;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentDto paymentDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var payment = await _paymentService.CreatePaymentAsync(
                paymentDto.BookingId, 
                userId, 
                paymentDto.PaymentMethod);

            if (payment == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to create payment. Booking may not exist."));

            return Ok(ApiResponse<PaymentDto>.SuccessResponse(payment, "Payment created. Please proceed to payment gateway."));
        }

        [HttpGet("booking/{bookingId}")]
        public async Task<IActionResult> GetPaymentByBooking(int bookingId)
        {
            var payment = await _paymentService.GetPaymentByBookingAsync(bookingId);
            if (payment == null)
                return NotFound(ApiResponse<object>.ErrorResponse("Payment not found"));

            return Ok(ApiResponse<PaymentDto>.SuccessResponse(payment));
        }

        [AllowAnonymous]
        [HttpPost("callback")]
        public async Task<IActionResult> PaymentCallback([FromBody] PaymentCallbackDto callbackDto)
        {
            if (string.IsNullOrEmpty(callbackDto.TransactionId))
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid callback data"));

            var status = callbackDto.Status.ToLower() == "success" 
                ? PaymentStatus.Completed 
                : PaymentStatus.Failed;

            var success = await _paymentService.UpdatePaymentStatusAsync(callbackDto.TransactionId, status);
            if (!success)
                return NotFound(ApiResponse<object>.ErrorResponse("Payment not found"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, $"Payment {status}"));
        }
    }
}
