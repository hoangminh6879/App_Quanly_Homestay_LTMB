using Microsoft.EntityFrameworkCore;
using Nhom1.Configurations;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IConversationService
    {
        Task<ConversationDto?> GetOrCreateConversationAsync(int bookingId, string userId);
        Task<List<ConversationDto>> GetUserConversationsAsync(string userId);
        Task<List<MessageDto>> GetConversationMessagesAsync(int conversationId, string userId);
        Task<MessageDto?> SendMessageAsync(int conversationId, string senderId, string content);
        Task<bool> MarkMessagesAsReadAsync(int conversationId, string userId);
        Task<int> GetUnreadCountAsync(string userId);
    }

    public class ConversationService : IConversationService
    {
        private readonly ApplicationDbContext _context;

        public ConversationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ConversationDto?> GetOrCreateConversationAsync(int bookingId, string userId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Homestay)
                .Include(b => b.User)
                .Include(b => b.Homestay.Host)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return null;

            // Check user is participant (either guest or host)
            if (booking.UserId != userId && booking.Homestay.HostId != userId)
                return null;

            // Check if conversation already exists for this booking
            var existingConversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Homestay)
                .FirstOrDefaultAsync(c => c.BookingId == bookingId);

            if (existingConversation != null)
            {
                return await MapToConversationDto(existingConversation, userId);
            }

            // Ensure consistent User1/User2 ordering (User1Id < User2Id)
            var user1Id = string.Compare(booking.UserId, booking.Homestay.HostId, StringComparison.Ordinal) < 0
                ? booking.UserId
                : booking.Homestay.HostId;
            var user2Id = string.Compare(booking.UserId, booking.Homestay.HostId, StringComparison.Ordinal) < 0
                ? booking.Homestay.HostId
                : booking.UserId;

            // Create new conversation
            var conversation = new Conversation
            {
                User1Id = user1Id,
                User2Id = user2Id,
                BookingId = bookingId,
                HomestayId = booking.HomestayId,
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            // Reload with navigation properties
            conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Homestay)
                .FirstAsync(c => c.Id == conversation.Id);

            return await MapToConversationDto(conversation, userId);
        }

        public async Task<List<ConversationDto>> GetUserConversationsAsync(string userId)
        {
            var conversations = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Homestay)
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .OrderByDescending(c => c.LastMessageAt)
                .ToListAsync();

            var result = new List<ConversationDto>();
            foreach (var conv in conversations)
            {
                result.Add(await MapToConversationDto(conv, userId));
            }

            return result;
        }

        public async Task<List<MessageDto>> GetConversationMessagesAsync(int conversationId, string userId)
        {
            var conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null) 
                return new List<MessageDto>();

            // Verify user is participant
            if (conversation.User1Id != userId && conversation.User2Id != userId)
                return new List<MessageDto>();

            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return messages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender.FullName,
                SenderAvatar = ImageHelper.GetUserAvatarUrl(m.Sender.ProfilePicture),
                Content = m.Content,
                SentAt = m.SentAt,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt,
                IsMine = m.SenderId == userId
            }).ToList();
        }

        public async Task<MessageDto?> SendMessageAsync(int conversationId, string senderId, string content)
        {
            var conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null) 
                return null;

            // Verify user is participant
            if (conversation.User1Id != senderId && conversation.User2Id != senderId)
                return null;

            // Determine receiver
            var receiverId = conversation.User1Id == senderId 
                ? conversation.User2Id 
                : conversation.User1Id;

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Type = MessageType.Text
            };

            _context.Messages.Add(message);

            // Update conversation's last message info
            conversation.LastMessage = content;
            conversation.LastMessageAt = message.SentAt;
            conversation.LastMessageSenderId = senderId;

            await _context.SaveChangesAsync();

            var sender = await _context.Users.FindAsync(senderId);
            return new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = sender?.FullName ?? "Unknown",
                SenderAvatar = ImageHelper.GetUserAvatarUrl(sender?.ProfilePicture),
                Content = message.Content,
                SentAt = message.SentAt,
                IsRead = message.IsRead,
                ReadAt = message.ReadAt,
                IsMine = true
            };
        }

        public async Task<bool> MarkMessagesAsReadAsync(int conversationId, string userId)
        {
            var conversation = await _context.Conversations.FindAsync(conversationId);
            if (conversation == null) 
                return false;

            // Verify user is participant
            if (conversation.User1Id != userId && conversation.User2Id != userId) 
                return false;

            var unreadMessages = await _context.Messages
                .Where(m => m.ConversationId == conversationId && 
                           m.ReceiverId == userId && 
                           !m.IsRead && 
                           !m.IsDeleted)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            if (unreadMessages.Any())
            {
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Messages
                .Where(m => m.ReceiverId == userId && !m.IsRead && !m.IsDeleted)
                .CountAsync();
        }

        private async Task<ConversationDto> MapToConversationDto(Conversation conversation, string currentUserId)
        {
            // Determine other participant
            var isUser1 = conversation.User1Id == currentUserId;
            var otherUser = isUser1 ? conversation.User2 : conversation.User1;
            var otherUserId = isUser1 ? conversation.User2Id : conversation.User1Id;

            // Count unread messages for current user
            var unreadCount = await _context.Messages
                .Where(m => m.ConversationId == conversation.Id && 
                           m.ReceiverId == currentUserId && 
                           !m.IsRead && 
                           !m.IsDeleted)
                .CountAsync();

            return new ConversationDto
            {
                Id = conversation.Id,
                BookingId = conversation.BookingId ?? 0,
                HomestayId = conversation.HomestayId ?? 0,
                HomestayName = conversation.Homestay?.Name ?? string.Empty,
                // Map to old HostId/GuestId format for API compatibility
                HostId = conversation.User1Id,
                HostName = conversation.User1?.FullName ?? string.Empty,
                HostAvatar = ImageHelper.GetUserAvatarUrl(conversation.User1?.ProfilePicture),
                GuestId = conversation.User2Id,
                GuestName = conversation.User2?.FullName ?? string.Empty,
                GuestAvatar = ImageHelper.GetUserAvatarUrl(conversation.User2?.ProfilePicture),
                CreatedAt = conversation.CreatedAt,
                LastMessageAt = conversation.LastMessageAt,
                LastMessage = conversation.LastMessage,
                LastMessageSenderId = conversation.LastMessageSenderId,
                UnreadCount = unreadCount
            };
        }
    }
}
