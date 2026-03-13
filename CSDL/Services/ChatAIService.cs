using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Nhom1.Services
{
    public interface IChatAIService
    {
        Task<string> GetResponseAsync(string message);
    }

    public class ChatAIService : IChatAIService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<ChatAIService> _logger;

        public ChatAIService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<ChatAIService> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetResponseAsync(string message)
        {
            // If Gemini is not configured, fall back to simple local generator
            var apiKey = _config["Gemini:ApiKey"];
            var baseUrl = _config["Gemini:BaseUrl"];
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
            {
                _logger.LogInformation("Gemini not configured, using local fallback generator.");
                return GenerateAIResponse(message);
            }

            try
            {
                var client = _httpFactory.CreateClient();
                client.BaseAddress = new Uri(baseUrl);

                // Prefer Authorization header; some deployments may require key in query string
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                // Build a minimal request body. The Google Generative Language API has slightly different shapes
                // We'll send { "prompt": { "text": "..." } } which works for many simple proxies.
                // Prepend a short homestay schema and guidance so the model knows how to introduce homestays.
                var homestaySeed =
                    "Bạn là trợ lý hiểu về cấu trúc dữ liệu homestay. Bảng homestay có các trường chính:\n" +
                    "- id (int)\n" +
                    "- name (string)\n" +
                    "- address (string)\n" +
                    "- city (string)\n" +
                    "- district (string)\n" +
                    "- pricePerNight (number)\n" +
                    "- currency (string)\n" +
                    "- amenities (list of strings)\n" +
                    "- hostName (string)\n" +
                    "- hostPhone (string)\n" +
                    "- rating (number, 0-5)\n" +
                    "- description (string)\n" +
                    "- images (list of urls)\n" +
                    "- capacity (int)\n\n" +
                    "Hướng dẫn: Khi được yêu cầu giới thiệu một homestay, hãy trả lời ngắn gọn bằng tiếng Việt, bao gồm: tên homestay, vị trí (thành phố/quận), giá trên đêm (định dạng rõ ràng), 2-3 điểm nổi bật từ danh sách tiện nghi, điểm đánh giá nếu có, và một câu kêu gọi hành động ngắn (ví dụ: cách đặt/liên hệ). Giữ trong 2-4 câu.";

                var promptText = homestaySeed + "\n\nUser: " + message;

                var payload = new
                {
                    prompt = new { text = promptText },
                    temperature = 0.2,
                    maxOutputTokens = 512
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await client.PostAsync(string.Empty, content);
                var respText = await res.Content.ReadAsStringAsync();
                _logger.LogInformation("ChatAIService Gemini response status: {Status}, body: {Body}", res.StatusCode, respText);

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini API returned non-success status {Status}, falling back.", res.StatusCode);
                    return GenerateAIResponse(message);
                }

                // Try to parse the response and extract a sensible text reply
                try
                {
                    using var doc = JsonDocument.Parse(respText);
                    // Common patterns: 'candidates' -> first -> 'content' array -> first -> 'text'
                    if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
                    {
                        var first = cands[0];
                        if (first.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array && contentArr.GetArrayLength() > 0)
                        {
                            var firstContent = contentArr[0];
                            if (firstContent.TryGetProperty("text", out var textProp))
                            {
                                return textProp.GetString() ?? GenerateAIResponse(message);
                            }
                        }
                    }

                    // Another pattern: 'output' -> 'content' -> first -> 'text'
                    if (doc.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Object)
                    {
                        if (output.TryGetProperty("content", out var outContent) && outContent.ValueKind == JsonValueKind.Array && outContent.GetArrayLength() > 0)
                        {
                            var fc = outContent[0];
                            if (fc.TryGetProperty("text", out var t2)) return t2.GetString() ?? GenerateAIResponse(message);
                        }
                    }

                    // OpenAI-like: choices[0].message.content or choices[0].text
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                    {
                        var ch0 = choices[0];
                        if (ch0.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                        {
                            return c.GetString() ?? GenerateAIResponse(message);
                        }

                        if (ch0.TryGetProperty("text", out var t3) && t3.ValueKind == JsonValueKind.String)
                        {
                            return t3.GetString() ?? GenerateAIResponse(message);
                        }
                    }

                    // As a last resort, try to find the first string anywhere in the JSON
                    var found = FindFirstStringValue(doc.RootElement);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing Gemini response, falling back to local generator.");
                    return GenerateAIResponse(message);
                }

                return GenerateAIResponse(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API, using fallback response.");
                return GenerateAIResponse(message);
            }
        }

        private static string FindFirstStringValue(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.String:
                    return el.GetString() ?? string.Empty;
                case JsonValueKind.Object:
                    foreach (var p in el.EnumerateObject())
                    {
                        var v = FindFirstStringValue(p.Value);
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var it in el.EnumerateArray())
                    {
                        var v = FindFirstStringValue(it);
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                    break;
            }
            return string.Empty;
        }

        // Copied fallback logic from WebHS (simple keyword based responses)
        private string GenerateAIResponse(string userMessage)
        {
            var message = userMessage.ToLower();
            if (message.Contains("tìm") || message.Contains("homestay") || message.Contains("đặt phòng"))
            {
                return "Để tìm homestay phù hợp, bạn có thể:\n• Sử dụng thanh tìm kiếm trên trang chủ\n• Lọc theo vị trí, giá cả, tiện nghi\n• Xem danh sách homestay nổi bật\n• Đọc đánh giá từ khách hàng khác\n\nBạn muốn tìm homestay ở đâu ạ?";
            }
            if (message.Contains("đặt") || message.Contains("booking") || message.Contains("thanh toán"))
            {
                return "Quy trình đặt phòng gồm:\n1. Chọn homestay và ngày ở\n2. Điền thông tin khách hàng\n3. Xác nhận đặt phòng\n4. Thanh toán online\n5. Nhận xác nhận qua email\n\nBạn cần hỗ trợ bước nào ạ?";
            }
            if (message.Contains("đăng nhập") || message.Contains("tài khoản") || message.Contains("đăng ký"))
            {
                return "Về tài khoản:\n• Đăng ký tài khoản để đặt phòng dễ dàng\n• Theo dõi lịch sử đặt phòng\n• Nhận thông báo và ưu đãi\n• Trở thành Host để cho thuê\n\nBạn cần hỗ trợ gì về tài khoản ạ?";
            }
            if (message.Contains("giá") || message.Contains("phí") || message.Contains("chi phí"))
            {
                return "Về giá cả:\n• Giá homestay tùy thuộc vào vị trí, tiện nghi\n• Có thể có phí dịch vụ và thuế\n• Áp dụng mã giảm giá nếu có\n• Thanh toán an toàn qua cổng thanh toán\n\nBạn muốn xem giá homestay nào ạ?";
            }
            if (message.Contains("hỗ trợ") || message.Contains("giúp") || message.Contains("liên hệ"))
            {
                return "Các cách liên hệ hỗ trợ:\n• Chat trực tiếp với tôi\n• Email: support@homestay.com\n• Hotline: 1900-xxxx\n• Fanpage Facebook\n\nTôi có thể giúp gì khác cho bạn?";
            }
            if (message.Contains("xin chào") || message.Contains("hello") || message.Contains("hi"))
            {
                return "Xin chào! Rất vui được hỗ trợ bạn. Tôi có thể giúp bạn tìm homestay, giải đáp thắc mắc về đặt phòng hoặc hướng dẫn sử dụng website. Bạn cần hỗ trợ gì ạ?";
            }
            if (message.Contains("cảm ơn") || message.Contains("thanks") || message.Contains("thank you"))
            {
                return "Rất vui được giúp đỡ bạn! Nếu có thêm câu hỏi nào khác, đừng ngại hỏi tôi nhé. Chúc bạn có trải nghiệm tuyệt vời với Homestay Booking! 😊";
            }

            var responses = new[]
            {
                "Tôi hiểu bạn đang hỏi về điều này. Bạn có thể cung cấp thêm chi tiết để tôi hỗ trợ tốt hơn không ạ?",
                "Đây là câu hỏi thú vị! Tôi sẽ cố gắng giúp bạn. Bạn có thể nói rõ hơn về vấn đề này không?",
                "Tôi có thể giúp bạn về việc tìm homestay, đặt phòng, hoặc sử dụng website. Bạn cần hỗ trợ vấn đề gì cụ thể ạ?",
                "Để tôi hỗ trợ bạn tốt nhất, bạn có thể nói rõ hơn về câu hỏi của mình không ạ?"
            };
            var rnd = new Random();
            return responses[rnd.Next(responses.Length)];
        }
    }
}
