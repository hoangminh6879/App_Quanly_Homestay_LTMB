using Microsoft.AspNetCore.Mvc;
using Nhom1.Services;
using System.Text.Json;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PayPalController : ControllerBase
    {
        private readonly IPayPalService _payPalService;

        public PayPalController(IPayPalService payPalService)
        {
            _payPalService = payPalService;
        }

        [HttpPost("create-order/{bookingId}")]
        public async Task<IActionResult> CreateOrder(int bookingId, [FromQuery] string? returnUrl = null, [FromQuery] string? cancelUrl = null)
        {
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _payPalService.CreateOrderAsync(bookingId, userId, returnUrl, cancelUrl);
            if (result == null) return BadRequest(new { message = "Could not create payment" });
            return Ok(result);
        }

        [HttpPost("capture-order/{orderId}/{bookingId}")]
        public async Task<IActionResult> CaptureOrder(string orderId, int bookingId)
        {
            var result = await _payPalService.CaptureOrderAsync(orderId, bookingId);
            if (result == null) return BadRequest(new { message = "Capture failed" });
            return Ok(result);
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            using var sr = new StreamReader(Request.Body);
            var body = await sr.ReadToEndAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var ok = await _payPalService.HandleWebhookAsync(root, Request.Headers);
            if (ok) return Ok();
            return BadRequest();
        }
    }
}
