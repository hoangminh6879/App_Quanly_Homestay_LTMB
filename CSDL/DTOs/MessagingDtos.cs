using System.ComponentModel.DataAnnotations;

namespace Nhom1.DTOs
{
    public class MessageViewModel
    {
        public int Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderAvatar { get; set; } = string.Empty;
        public string ReceiverId { get; set; } = string.Empty;
        public string ReceiverName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentFileName { get; set; }
        public bool IsSentByCurrentUser { get; set; }

        public int? HomestayId { get; set; }
        public string? HomestayName { get; set; }
        public int? BookingId { get; set; }
    }

    public class ConversationViewModel
    {
        public int Id { get; set; }
        public string ParticipantId { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string ParticipantAvatar { get; set; } = string.Empty;
        public string ParticipantRole { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; }
        public string? LastMessage { get; set; }
        public string? LastMessageSenderId { get; set; }
        public bool HasUnreadMessages { get; set; }
        public int UnreadCount { get; set; }

        public int? HomestayId { get; set; }
        public string? HomestayName { get; set; }
        public int? BookingId { get; set; }
        public string? Subject { get; set; }
    }

    public class StartConversationDto
    {
        // optional: start by booking to preserve existing flow
        public int? BookingId { get; set; }
        public string WithUserId { get; set; } = string.Empty;
        public string InitialMessage { get; set; } = string.Empty;
    }

    public class SendMessageDto
    {
        [Required]
        [StringLength(1000)]
        public string Content { get; set; } = string.Empty;
    }
}
