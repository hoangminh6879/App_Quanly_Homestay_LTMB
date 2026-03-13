using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IReviewService
    {
        Task<List<ReviewDto>> GetHomestayReviewsAsync(int homestayId, int page = 1, int pageSize = 10);
        Task<bool> AddHostResponseAsync(int bookingId, string hostId, string response);
        Task<bool> CreateReviewAsync(int bookingId, string userId, int rating, string? comment);
    }

    public class ReviewService : IReviewService
    {
        private readonly ApplicationDbContext _context;

        public ReviewService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReviewDto>> GetHomestayReviewsAsync(int homestayId, int page = 1, int pageSize = 10)
        {
            var reviews = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Homestay)
                .Where(b => b.HomestayId == homestayId && 
                           b.ReviewRating.HasValue && 
                           b.ReviewIsActive)
                .OrderByDescending(b => b.ReviewCreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new ReviewDto
                {
                    BookingId = b.Id,
                    HomestayId = b.HomestayId,
                    HomestayName = b.Homestay.Name,
                    UserName = b.User.FullName,
                    UserAvatar = b.User.ProfilePicture,
                    Rating = b.ReviewRating!.Value,
                    Comment = b.ReviewComment ?? "",
                    CreatedAt = b.ReviewCreatedAt ?? DateTime.UtcNow,
                    HostResponse = null,
                    HostResponseDate = null
                })
                .ToListAsync();

            return reviews;
        }

        public async Task<bool> AddHostResponseAsync(int bookingId, string hostId, string response)
        {
            // Host response feature not supported in current model
            // Can be implemented later if needed
            return await Task.FromResult(false);
        }

        public async Task<bool> CreateReviewAsync(int bookingId, string userId, int rating, string? comment)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null || booking.Status != BookingStatus.Completed)
                return false;

            booking.ReviewRating = rating;
            booking.ReviewComment = comment;
            booking.ReviewCreatedAt = DateTime.UtcNow;
            booking.ReviewIsActive = true;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
