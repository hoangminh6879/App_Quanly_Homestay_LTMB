using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nhom1.Services;
using Nhom1.DTOs;
using Microsoft.Extensions.Configuration;

namespace Nhom1.Controllers
{
    // Lightweight controller that mirrors the WebHS PaymentController route for local/dev testing.
    // Exposes /Payment/ProcessPayment so the mobile client can POST to that path (form or JSON)
    [Authorize]
    [Route("/Payment")]
    public class PaymentController : Controller
    {
        private readonly IPaymentService _paymentService;
        private readonly IPayPalService _payPalService;
        private readonly IConfiguration _configuration;

        public PaymentController(IPaymentService paymentService, IPayPalService payPalService, IConfiguration configuration)
        {
            _paymentService = paymentService;
            _payPalService = payPalService;
            _configuration = configuration;
        }

        [HttpPost("ProcessPayment")]
        [AllowAnonymous]
    public async Task<IActionResult> ProcessPayment([FromForm] int bookingId, [FromForm] string paymentMethod)
        {
            // Accept both JSON body and form-encoded POSTs
            if (bookingId == 0 || string.IsNullOrEmpty(paymentMethod))
            {
                // Try to read from JSON body as fallback
                try
                {
                    var body = await HttpContext.Request.ReadFromJsonAsync<CreatePaymentDto>();
                    if (body != null)
                    {
                        bookingId = body.BookingId;
                        paymentMethod = body.PaymentMethod;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (bookingId == 0 || string.IsNullOrEmpty(paymentMethod))
            {
                return Json(new { success = false, message = "Invalid bookingId or paymentMethod" });
            }

            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            // For FREE payment, create payment and mark as completed immediately
            if (paymentMethod.Equals("FREE", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var freePayment = await _paymentService.CreatePaymentAsync(bookingId, userId, "FREE");
                    if (freePayment == null)
                    {
                        return Json(new { success = false, message = "Failed to create payment record" });
                    }

                    // Mark as completed immediately for free payment
                    await _paymentService.UpdatePaymentStatusByBookingAsync(bookingId, null, Nhom1.Models.PaymentStatus.Completed);
                    
                    return Json(new { success = true, paymentUrl = "/payment-result?success=true&bookingId=" + bookingId });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Free payment error: {ex.Message}" });
                }
            }

            // For PayPal, use PayPalService to create order and get real approve link
            if (paymentMethod.Equals("PayPal", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                        var returnUrl = Url.Action("PaymentReturn", "Payment", new { bookingId }, Request.Scheme);
                        var cancelUrl = Url.Action("PaymentCancel", "Payment", null, Request.Scheme);

                        // If running on a device the generated localhost URL won't be reachable.
                        // Prefer configured PayPal Return/Cancel URLs if provided in config (useful for mobile testing or ngrok).
                        var cfgReturn = _configuration["PaymentSettings:PayPal:ReturnUrl"] ?? _configuration["PayPal:ReturnUrl"];
                        var cfgCancel = _configuration["PaymentSettings:PayPal:CancelUrl"] ?? _configuration["PayPal:CancelUrl"];

                        if (!string.IsNullOrEmpty(cfgReturn))
                        {
                            // Allow token replacement like {bookingId} in config; otherwise append bookingId as query param.
                            if (cfgReturn.Contains("{bookingId}")) cfgReturn = cfgReturn.Replace("{bookingId}", bookingId.ToString());
                            else cfgReturn = cfgReturn + (cfgReturn.Contains("?") ? "&" : "?") + $"bookingId={bookingId}";
                            returnUrl = cfgReturn;
                        }

                        if (!string.IsNullOrEmpty(cfgCancel))
                        {
                            if (cfgCancel.Contains("{bookingId}")) cfgCancel = cfgCancel.Replace("{bookingId}", bookingId.ToString());
                            else cfgCancel = cfgCancel + (cfgCancel.Contains("?") ? "&" : "?") + $"bookingId={bookingId}";
                            cancelUrl = cfgCancel;
                        }

                        var result = await _payPalService.CreateOrderAsync(bookingId, userId, returnUrl, cancelUrl);
                    if (result == null)
                    {
                        return Json(new { success = false, message = "Failed to create PayPal order" });
                    }

                    // Normalize result by serializing -> parse JSON to extract OrderId/ApproveLink robustly
                    string resultJson;
                    try
                    {
                        resultJson = System.Text.Json.JsonSerializer.Serialize(result);
                    }
                    catch
                    {
                        // fallback to ToString()
                        resultJson = result.ToString() ?? string.Empty;
                    }

                    string? orderId = null;
                    string? approveLink = null;

                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
                        var root = doc.RootElement;
                        if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (root.TryGetProperty("OrderId", out var idProp)) orderId = idProp.GetString();
                            if (root.TryGetProperty("ApproveLink", out var linkProp)) approveLink = linkProp.GetString();
                        }
                    }
                    catch (Exception ex)
                    {
                        var l = HttpContext.RequestServices.GetService(typeof(ILogger<PaymentController>)) as ILogger;
                        l?.LogWarning(ex, "Failed to parse PayPal service result JSON: {ResultJson}", resultJson);
                    }

                    if (string.IsNullOrEmpty(approveLink))
                    {
                        return Json(new { success = false, message = "No PayPal approve link" });
                    }

                    return Json(new { success = true, paymentUrl = approveLink });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"PayPal error: {ex.Message}" });
                }
            }

            // For other payment methods, create payment record and simulate URL
            var payment = await _paymentService.CreatePaymentAsync(bookingId, userId, paymentMethod);
            var token = payment?.TransactionId ?? $"DEV_{Guid.NewGuid():N}";
            var paymentUrl = $"https://example.local/payment-simulator?token={token}&PayerID=TESTPAYER";

            return Json(new { success = true, paymentUrl });
        }

        [HttpGet("PaymentReturn")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentReturn(string? orderId, int bookingId, string? token, string? PayerID)
        {
            // PayPal redirects here after user approves payment with query params: token, PayerID
            // We need to capture the order
            if (string.IsNullOrEmpty(orderId) && !string.IsNullOrEmpty(token))
            {
                // PayPal uses 'token' as orderId in sandbox
                orderId = token;
            }

            if (string.IsNullOrEmpty(orderId))
            {
                return Redirect($"/payment-result?success=false&message=Missing order ID");
            }

            try
            {
                // Log incoming query for diagnostics
                var q = Request.QueryString.HasValue ? Request.QueryString.Value : "";
                // Use ILogger by resolving from HttpContext.RequestServices
                var logger = HttpContext.RequestServices.GetService(typeof(ILogger<PaymentController>)) as ILogger;
                logger?.LogInformation("PaymentReturn called with orderId={OrderId}, token={Token}, PayerID={PayerId}, query={Query}", orderId, token, PayerID, q);

                var captureResult = await _payPalService.CaptureOrderAsync(orderId, bookingId);
                if (captureResult == null)
                {
                    logger?.LogWarning("Capture returned null for order {OrderId}, booking {BookingId}", orderId, bookingId);
                    return Redirect($"/payment-result?success=false&message=Capture failed");
                }

                // Success - redirect to success page
                logger?.LogInformation("Capture succeeded for order {OrderId}, booking {BookingId}", orderId, bookingId);
                return Redirect($"/payment-result?success=true&bookingId={bookingId}");
            }
            catch (Exception ex)
            {
                var logger = HttpContext.RequestServices.GetService(typeof(ILogger<PaymentController>)) as ILogger;
                logger?.LogError(ex, "Exception in PaymentReturn for orderId={OrderId}, bookingId={BookingId}", orderId, bookingId);
                return Redirect($"/payment-result?success=false&message={Uri.EscapeDataString(ex.Message)}");
            }
        }

        [HttpGet("PaymentCancel")]
        [AllowAnonymous]
        public IActionResult PaymentCancel()
        {
            return Redirect("/payment-result?success=false&cancelled=true");
        }

        [HttpGet("payment-result")]
        [AllowAnonymous]
        public IActionResult PaymentResult(bool success, bool cancelled = false, string? message = null, int? bookingId = null)
        {
            // Simple HTML page for mobile WebView to detect
            var status = success ? "success" : (cancelled ? "cancelled" : "failed");
            var displayMessage = message ?? (success ? "Payment completed successfully!" : 
                                            cancelled ? "Payment was cancelled." : 
                                            "Payment failed. Please try again.");

            return Content($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Payment Result</title>
    <style>
        body {{
            font-family: system-ui, -apple-system, sans-serif;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
            padding: 20px;
            background: {(success ? "#e8f5e9" : cancelled ? "#fff3e0" : "#ffebee")};
        }}
        .result-card {{
            background: white;
            border-radius: 16px;
            padding: 32px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.1);
            text-align: center;
            max-width: 400px;
        }}
        .icon {{
            font-size: 64px;
            margin-bottom: 16px;
        }}
        h1 {{
            margin: 0 0 8px 0;
            color: {(success ? "#2e7d32" : cancelled ? "#f57c00" : "#c62828")};
        }}
        p {{
            color: #666;
            margin: 0;
        }}
    </style>
</head>
<body>
    <div class='result-card'>
        <div class='icon'>{(success ? "✅" : cancelled ? "⚠️" : "❌")}</div>
        <h1>Payment {status.ToUpper()}</h1>
        <p>{displayMessage}</p>
        {(bookingId.HasValue ? $"<p style='margin-top: 12px; font-size: 14px;'>Booking ID: {bookingId.Value}</p>" : "")}
    </div>
</body>
</html>
", "text/html");
        }
    }
}
