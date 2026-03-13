using Microsoft.AspNetCore.Mvc;
using Nhom1.Services;

namespace Nhom1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatAIController : ControllerBase
    {
        private readonly ILogger<ChatAIController> _logger;
        private readonly IChatAIService _chatAIService;

        public ChatAIController(ILogger<ChatAIController> logger, IChatAIService chatAIService)
        {
            _logger = logger;
            _chatAIService = chatAIService;
        }

        [HttpPost("message")]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest("Message cannot be empty");
                }

                var response = await _chatAIService.GetResponseAsync(request.Message);

                return Ok(new ChatMessageResponse
                {
                    Message = response,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class ChatMessageRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public class ChatMessageResponse
    {
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
