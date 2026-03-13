using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Nhom1.DTOs;
using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IPayPalService
    {
        Task<object?> CreateOrderAsync(int bookingId, string userId, string? returnUrl = null, string? cancelUrl = null);
        Task<object?> CaptureOrderAsync(string orderId, int bookingId);
        Task<bool> HandleWebhookAsync(JsonElement webhookEvent, IHeaderDictionary headers);
    }

    public class PayPalService : IPayPalService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PayPalService> _logger;

        public PayPalService(IHttpClientFactory httpFactory, IConfiguration config, IPaymentService paymentService, ILogger<PayPalService> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _paymentService = paymentService;
            _logger = logger;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var clientId = _config["PayPal:ClientId"]!;
            var secret = _config["PayPal:Secret"]!;
            var baseUrl = _config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";

            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);

            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));

            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            req.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });

            var res = await client.SendAsync(req);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString()!;
        }

        public async Task<object?> CreateOrderAsync(int bookingId, string userId, string? returnUrl = null, string? cancelUrl = null)
        {
            try
            {
                // Create or get payment record
                var paymentDto = await _paymentService.CreatePaymentAsync(bookingId, userId, "PayPal");
                if (paymentDto == null)
                {
                    _logger.LogError("Failed to create payment record for booking {BookingId}", bookingId);
                    return null;
                }

                var accessToken = await GetAccessTokenAsync();
                var baseUrl = _config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";
                var client = _httpFactory.CreateClient();
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Build order request using payment amount
                var body = new
                {
                    intent = "CAPTURE",
                    purchase_units = new[] {
                        new {
                            amount = new { currency_code = "USD", value = paymentDto.Amount.ToString("F2") }
                        }
                    },
                    application_context = new
                    {
                        return_url = returnUrl,
                        cancel_url = cancelUrl
                    }
                };

                _logger.LogInformation("Creating PayPal order for booking {BookingId}, amount: {Amount}", bookingId, paymentDto.Amount);

                var req = new HttpRequestMessage(HttpMethod.Post, "/v2/checkout/orders");
                req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                var res = await client.SendAsync(req);
                var respText = await res.Content.ReadAsStringAsync();
                
                _logger.LogInformation("PayPal API Response Status: {StatusCode}", res.StatusCode);
                _logger.LogInformation("PayPal API Response: {Response}", respText);

                res.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(respText);
                var root = doc.RootElement;
                var orderId = root.GetProperty("id").GetString();

                // Return order id and approve link (if present)
                string? approveLink = null;
                if (root.TryGetProperty("links", out var links))
                {
                    foreach (var l in links.EnumerateArray())
                    {
                        var rel = l.GetProperty("rel").GetString();
                        var href = l.GetProperty("href").GetString();
                        _logger.LogInformation("PayPal link: rel={Rel}, href={Href}", rel, href);
                        
                        if (rel == "approve")
                        {
                            approveLink = href;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(approveLink))
                {
                    _logger.LogWarning("No approve link found in PayPal response for order {OrderId}", orderId);
                }

                return new { OrderId = orderId, ApproveLink = approveLink };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PayPal order for booking {BookingId}", bookingId);
                throw;
            }
        }

        public async Task<object?> CaptureOrderAsync(string orderId, int bookingId)
        {
            var accessToken = await GetAccessTokenAsync();
            var baseUrl = _config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com";
            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage res;
            string respText = string.Empty;
            try
            {
                res = await client.PostAsync($"/v2/checkout/orders/{orderId}/capture", null);
                respText = await res.Content.ReadAsStringAsync();
                _logger.LogInformation("PayPal Capture Response Status: {StatusCode}", res.StatusCode);
                _logger.LogInformation("PayPal Capture Response: {Response}", respText);
                res.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing PayPal order {OrderId} for booking {BookingId}. Response: {ResponseSnippet}", orderId, bookingId, respText);
                throw;
            }

            using var doc = JsonDocument.Parse(respText);
            var root = doc.RootElement;

            // Update payment status based on capture
            // Try to find transaction or invoice id if available
            string? transactionId = null;
            if (root.TryGetProperty("purchase_units", out var pus))
            {
                foreach (var pu in pus.EnumerateArray())
                {
                    if (pu.TryGetProperty("payments", out var payments) && payments.TryGetProperty("captures", out var caps))
                    {
                        foreach (var cap in caps.EnumerateArray())
                        {
                            if (cap.TryGetProperty("id", out var tid))
                            {
                                transactionId = tid.GetString();
                                break;
                            }
                        }
                    }
                }
            }

            // Update payment status by bookingId (not by transactionId which may not match)
            await _paymentService.UpdatePaymentStatusByBookingAsync(bookingId, transactionId, PaymentStatus.Completed);

            return root;
        }

        public async Task<bool> HandleWebhookAsync(JsonElement webhookEvent, IHeaderDictionary headers)
        {
            // Optional: verify webhook signature using /v1/notifications/verify-webhook-signature
            // For now, process common events
            try
            {
                if (!webhookEvent.TryGetProperty("event_type", out var et)) return false;
                var eventType = et.GetString();

                if (eventType == "PAYMENT.CAPTURE.COMPLETED" || eventType == "CHECKOUT.ORDER.APPROVED")
                {
                    // Extract transaction id
                    if (webhookEvent.TryGetProperty("resource", out var resource))
                    {
                        string? transactionId = null;
                        if (resource.TryGetProperty("id", out var rid)) transactionId = rid.GetString();

                        if (!string.IsNullOrEmpty(transactionId))
                        {
                            await _paymentService.UpdatePaymentStatusAsync(transactionId, PaymentStatus.Completed);
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
