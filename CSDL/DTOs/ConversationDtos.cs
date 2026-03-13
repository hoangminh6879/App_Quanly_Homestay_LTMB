using System.ComponentModel.DataAnnotations;

namespace Nhom1.DTOs
{
    public class ConversationDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int HomestayId { get; set; }
        public string HomestayName { get; set; } = string.Empty;
        public string HostId { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string HostAvatar { get; set; } = string.Empty;
        public string GuestId { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string GuestAvatar { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public string? LastMessage { get; set; }
        public string? LastMessageSenderId { get; set; }
        public int UnreadCount { get; set; }
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public int? ConversationId { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderAvatar { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsMine { get; set; }
    }

    public class CreateMessageDto
    {
        [Required]
        [StringLength(1000)]
        public string Content { get; set; } = string.Empty;
    }
}