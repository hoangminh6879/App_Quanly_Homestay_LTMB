using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nhom1.DTOs;
using Nhom1.Services;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly ILogger<ConversationsController> _logger;

        public ConversationsController(IConversationService conversationService, ILogger<ConversationsController> logger)
        {
            _conversationService = conversationService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var conversations = await _conversationService.GetUserConversationsAsync(userId);

                // Map ConversationDto -> ConversationViewModel
                var convs = conversations.Select(c => new ConversationViewModel
                {
                    Id = c.Id,
                    ParticipantId = (c.HostId == userId) ? c.GuestId : c.HostId,
                    ParticipantName = (c.HostId == userId) ? c.GuestName : c.HostName,
                    ParticipantAvatar = (c.HostId == userId) ? c.GuestAvatar : c.HostAvatar,
                    LastMessageAt = c.LastMessageAt ?? c.CreatedAt,
                    LastMessage = c.LastMessage,
                    LastMessageSenderId = c.LastMessageSenderId,
                    UnreadCount = c.UnreadCount,
                    HasUnreadMessages = c.UnreadCount > 0,
                    HomestayId = c.HomestayId,
                    HomestayName = c.HomestayName,
                    BookingId = c.BookingId
                }).ToList();

                return Ok(ApiResponse<List<ConversationViewModel>>.SuccessResponse(convs));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting conversations for user {UserId}", userId);
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Server error: {ex.Message}"));
            }
        }

        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] StartConversationDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // If BookingId provided, keep existing GetOrCreateChat flow
            if (dto.BookingId.HasValue)
            {
                var chat = await _conversationService.GetOrCreateConversationAsync(dto.BookingId.Value, userId);
                if (chat == null)
                    return BadRequest(ApiResponse<object>.ErrorResponse("Invalid booking or cannot start conversation"));

                var conv = new ConversationViewModel
                {
                    Id = chat.Id,
                    ParticipantId = (chat.HostId == userId) ? chat.GuestId : chat.HostId,
                    ParticipantName = (chat.HostId == userId) ? chat.GuestName : chat.HostName,
                    ParticipantAvatar = (chat.HostId == userId) ? chat.GuestAvatar : chat.HostAvatar,
                    LastMessageAt = chat.LastMessageAt ?? chat.CreatedAt,
                    LastMessage = chat.LastMessage,
                    HomestayId = chat.HomestayId,
                    BookingId = chat.BookingId
                };

                return Ok(ApiResponse<ConversationViewModel>.SuccessResponse(conv));
            }

            // Otherwise, not implemented: creation by user id (requires more permissions and checks)
            return BadRequest(ApiResponse<object>.ErrorResponse("Start by booking only is supported currently."));
        }

        [HttpGet("{conversationId}/messages")]
        public async Task<IActionResult> Messages(int conversationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var messages = await _conversationService.GetConversationMessagesAsync(conversationId, userId);

            // Map MessageDto -> MessageViewModel
            var result = messages.Select(m => new MessageViewModel
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                SenderAvatar = m.SenderAvatar,
                Content = m.Content,
                SentAt = m.SentAt,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt,
                IsSentByCurrentUser = m.IsMine
            }).ToList();

            return Ok(ApiResponse<List<MessageViewModel>>.SuccessResponse(result));
        }

        [HttpPost("{conversationId}/messages")]
        public async Task<IActionResult> SendMessage(int conversationId, [FromBody] SendMessageDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ErrorResponse("Invalid message data"));

            var message = await _conversationService.SendMessageAsync(conversationId, userId, dto.Content);
            if (message == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to send message or conversation not found."));

            var vm = new MessageViewModel
            {
                Id = message.Id,
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                SenderAvatar = message.SenderAvatar,
                Content = message.Content,
                SentAt = message.SentAt,
                IsRead = message.IsRead,
                ReadAt = message.ReadAt,
                IsSentByCurrentUser = true
            };

            return Ok(ApiResponse<MessageViewModel>.SuccessResponse(vm, "Message sent successfully"));
        }

        [HttpPost("{conversationId}/read")]
        public async Task<IActionResult> MarkRead(int conversationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _conversationService.MarkMessagesAsReadAsync(conversationId, userId);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to mark messages as read"));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Messages marked as read"));
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var count = await _conversationService.GetUnreadCountAsync(userId);
            return Ok(ApiResponse<object>.SuccessResponse(new { unreadCount = count }));
        }
    }
}
