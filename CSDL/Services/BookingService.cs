using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IBookingService
    {
        Task<BookingDto?> CreateBookingAsync(CreateBookingDto createDto, string userId);
        Task<BookingDto?> GetBookingByIdAsync(int id, string userId);
        Task<List<BookingDto>> GetUserBookingsAsync(string userId);
        Task<List<BookingDto>> GetHostBookingsAsync(string hostId);
        Task<int> GetBookingsCountByHostAsync(string hostId);
        Task<decimal> GetTotalRevenueByHostAsync(string hostId);
        Task<decimal> GetCurrentMonthRevenueByHostAsync(string hostId);
        Task<int> GetCompletedBookingsCountByHostAsync(string hostId);
        Task<List<object>> GetMonthlyRevenueByHostAsync(string hostId, int months);
        Task<bool> UpdateBookingStatusAsync(int id, BookingStatus status, string userId);
        Task<bool> CancelBookingAsync(int id, string userId);
        Task<bool> CreateReviewAsync(int bookingId, CreateReviewDto reviewDto, string userId);
        Task<AvailabilityResponseDto> CheckAvailabilityAsync(BookingAvailabilityDto availabilityDto);
        Task<List<DateTime>> GetBookedDatesAsync(int homestayId);
        Task<AmountCalculationDto> CalculateBookingAmountAsync(int homestayId, DateTime checkIn, DateTime checkOut, string? promotionCode = null);
    }

    public class BookingService : IBookingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IMailService _mailService;
        private readonly IPromotionService _promotionService;

        public BookingService(ApplicationDbContext context, IConfiguration config, IMailService mailService, IPromotionService promotionService)
        {
            _context = context;
            _config = config;
            _mailService = mailService;
            _promotionService = promotionService;
        }

    public async Task<BookingDto?> CreateBookingAsync(CreateBookingDto createDto, string userId)
        {
            // 1. Validate that check-in date is not in the past
            if (createDto.CheckInDate.Date < DateTime.Today)
                return null;

            // 2. Validate date order and minimum stay
            if (createDto.CheckOutDate <= createDto.CheckInDate)
                return null;

            var nights = (createDto.CheckOutDate - createDto.CheckInDate).Days;
            if (nights < 1)
                return null;

            // 3. Validate homestay exists and is active
            var homestay = await _context.Homestays.FindAsync(createDto.HomestayId);
            if (homestay == null || !homestay.IsActive || !homestay.IsApproved)
                return null;

            // 4. Check for blocked dates
            var hasBlockedDates = await _context.BlockedDates.AnyAsync(bd =>
                bd.HomestayId == createDto.HomestayId &&
                bd.Date >= createDto.CheckInDate &&
                bd.Date < createDto.CheckOutDate);

            if (hasBlockedDates)
                return null;

            // 5. Check for conflicting bookings
            var isAvailable = !await _context.Bookings.AnyAsync(b =>
                b.HomestayId == createDto.HomestayId &&
                b.Status != BookingStatus.Cancelled &&
                b.CheckInDate < createDto.CheckOutDate &&
                b.CheckOutDate > createDto.CheckInDate);

            if (!isAvailable)
                return null;

            // Calculate amount
            var subtotal = homestay.PricePerNight * nights;
            decimal discount = 0m;
            Promotion? appliedPromotion = null;

            if (!string.IsNullOrEmpty(createDto.PromotionCode))
            {
                // Validate promotion but do not increment usage yet — usage increments only when payment confirmed
                appliedPromotion = await _promotionService.GetEntityByCodeIfApplicableAsync(createDto.PromotionCode, subtotal);
                if (appliedPromotion != null)
                {
                    var calc = await _promotionService.CalculateAmountWithPromotionAsync(appliedPromotion, subtotal);
                    discount = calc.Discount;
                }
            }

            var totalAmount = subtotal;

            var autoConfirm = _config.GetValue<bool>("FeatureFlags:AutoConfirmBookings");

            var booking = new Booking
            {
                HomestayId = createDto.HomestayId,
                UserId = userId,
                CheckInDate = createDto.CheckInDate,
                CheckOutDate = createDto.CheckOutDate,
                NumberOfGuests = createDto.NumberOfGuests,
                TotalAmount = totalAmount,
                DiscountAmount = Math.Round(discount, 2),
                FinalAmount = Math.Round(totalAmount - discount, 2),
                PromotionId = appliedPromotion?.Id,
                Status = autoConfirm ? BookingStatus.Paid : BookingStatus.Pending,
                Notes = createDto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            // If auto-confirm enabled, create blocked dates immediately
            if (autoConfirm)
            {
                var blockedDates = new List<BlockedDate>();
                var currentDate = booking.CheckInDate.Date;
                while (currentDate < booking.CheckOutDate.Date)
                {
                    blockedDates.Add(new BlockedDate
                    {
                        HomestayId = booking.HomestayId,
                        Date = currentDate,
                        Reason = $"Booking #{booking.Id} - {booking.NumberOfGuests} guests",
                        CreatedAt = DateTime.UtcNow
                    });
                    currentDate = currentDate.AddDays(1);
                }

                if (blockedDates.Any())
                {
                    _context.BlockedDates.AddRange(blockedDates);
                    await _context.SaveChangesAsync();
                }

                // Send booking confirmation email asynchronously (do not block creation)
                try
                {
                    var bookingWithDetails = await _context.Bookings
                        .Include(b => b.User)
                        .Include(b => b.Homestay)
                        .FirstOrDefaultAsync(b => b.Id == booking.Id);

                    if (bookingWithDetails != null)
                    {
                        _ = _mailService.SendBookingConfirmationAsync(bookingWithDetails);
                    }
                }
                catch
                {
                    // swallow - logging within mail service
                }

                // If promotion applied and auto-confirmed (immediate Paid), increment usage count but do not fail booking on error
                if (appliedPromotion != null)
                {
                    try
                    {
                        await _promotionService.IncrementUsageAsync(appliedPromotion.Id);
                    }
                    catch
                    {
                        // swallow errors to avoid blocking booking flow
                    }
                }
            }

            return await GetBookingByIdAsync(booking.Id, userId);
        }

        public async Task<BookingDto?> GetBookingByIdAsync(int id, string userId)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Homestay)
                    .ThenInclude(h => h.Images)
                .FirstOrDefaultAsync(b => b.Id == id && (b.UserId == userId || b.Homestay.HostId == userId));

            return booking != null ? MapToDto(booking) : null;
        }

        public async Task<List<BookingDto>> GetUserBookingsAsync(string userId)
        {
            // materialize entity results first, then map to DTOs in memory to avoid EF client-projection issues
            var bookings = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Homestay)
                    .ThenInclude(h => h.Images)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return bookings.Select(b => MapToDto(b)).ToList();
        }

        public async Task<List<BookingDto>> GetHostBookingsAsync(string hostId)
        {
            // materialize entity results first, then map to DTOs in memory to avoid EF client-projection issues
            var bookings = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Homestay)
                    .ThenInclude(h => h.Images)
                .Where(b => b.Homestay.HostId == hostId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return bookings.Select(b => MapToDto(b)).ToList();
        }

        public async Task<int> GetBookingsCountByHostAsync(string hostId)
        {
            return await _context.Bookings
                .Where(b => b.Homestay.HostId == hostId)
                .CountAsync();
        }

        public async Task<decimal> GetTotalRevenueByHostAsync(string hostId)
        {
            return await _context.Bookings
                .Where(b => b.Homestay.HostId == hostId && b.Status == BookingStatus.Completed)
                .SumAsync(b => b.FinalAmount);
        }

        public async Task<decimal> GetCurrentMonthRevenueByHostAsync(string hostId)
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            return await _context.Bookings
                .Where(b => b.Homestay.HostId == hostId &&
                           b.Status == BookingStatus.Completed &&
                           b.CreatedAt.Month == currentMonth &&
                           b.CreatedAt.Year == currentYear)
                .SumAsync(b => b.FinalAmount);
        }

        public async Task<int> GetCompletedBookingsCountByHostAsync(string hostId)
        {
            return await _context.Bookings
                .Where(b => b.Homestay.HostId == hostId && b.Status == BookingStatus.Completed)
                .CountAsync();
        }

        public async Task<List<object>> GetMonthlyRevenueByHostAsync(string hostId, int months)
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddMonths(-months + 1);

            var monthlyRevenue = await _context.Bookings
                .Where(b => b.Homestay.HostId == hostId &&
                           b.Status == BookingStatus.Completed &&
                           b.CreatedAt >= startDate &&
                           b.CreatedAt <= endDate)
                .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(b => b.FinalAmount)
                })
                .OrderByDescending(m => m.Year)
                .ThenByDescending(m => m.Month)
                .ToListAsync();

            return monthlyRevenue.Select(m => new
            {
                year = m.Year,
                month = m.Month,
                revenue = m.Revenue
            }).ToList<object>();
        }

        public async Task<bool> UpdateBookingStatusAsync(int id, BookingStatus status, string userId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Homestay)
                .FirstOrDefaultAsync(b => b.Id == id && b.Homestay.HostId == userId);

            if (booking == null)
                return false;

            booking.Status = status;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CancelBookingAsync(int id, string userId)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null || booking.Status == BookingStatus.Cancelled)
                return false;

            booking.Status = BookingStatus.Cancelled;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> CreateReviewAsync(int bookingId, CreateReviewDto reviewDto, string userId)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId && b.Status == BookingStatus.Completed);

            if (booking == null || booking.ReviewRating.HasValue)
                return false;

            booking.ReviewRating = reviewDto.Rating;
            booking.ReviewComment = reviewDto.Comment;
            booking.ReviewCreatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<AvailabilityResponseDto> CheckAvailabilityAsync(BookingAvailabilityDto availabilityDto)
        {
            // Check booking conflicts
            var hasConflict = await _context.Bookings.AnyAsync(b =>
                b.HomestayId == availabilityDto.HomestayId &&
                b.Status != BookingStatus.Cancelled &&
                b.CheckInDate < availabilityDto.CheckOutDate &&
                b.CheckOutDate > availabilityDto.CheckInDate);

            if (hasConflict)
            {
                return new AvailabilityResponseDto
                {
                    IsAvailable = false,
                    Message = "Homestay is already booked for selected dates"
                };
            }

            // Check blocked dates
            var hasBlockedDates = await _context.BlockedDates.AnyAsync(bd =>
                bd.HomestayId == availabilityDto.HomestayId &&
                bd.Date >= availabilityDto.CheckInDate &&
                bd.Date < availabilityDto.CheckOutDate);

            if (hasBlockedDates)
            {
                var blockedDates = await _context.BlockedDates
                    .Where(bd => bd.HomestayId == availabilityDto.HomestayId &&
                                bd.Date >= availabilityDto.CheckInDate &&
                                bd.Date < availabilityDto.CheckOutDate)
                    .Select(bd => bd.Date)
                    .ToListAsync();

                return new AvailabilityResponseDto
                {
                    IsAvailable = false,
                    Message = "Some dates are blocked by the host",
                    UnavailableDates = blockedDates
                };
            }

            return new AvailabilityResponseDto
            {
                IsAvailable = true,
                Message = "Homestay is available"
            };
        }

        private BookingDto MapToDto(Booking booking)
        {
            return new BookingDto
            {
                Id = booking.Id,
                CheckInDate = booking.CheckInDate,
                CheckOutDate = booking.CheckOutDate,
                NumberOfGuests = booking.NumberOfGuests,
                NumberOfNights = booking.NumberOfNights,
                TotalAmount = booking.TotalAmount,
                DiscountAmount = booking.DiscountAmount,
                FinalAmount = booking.FinalAmount,
                Status = booking.Status,
                Notes = booking.Notes,
                CreatedAt = booking.CreatedAt,
                ReviewRating = booking.ReviewRating,
                ReviewComment = booking.ReviewComment,
                ReviewCreatedAt = booking.ReviewCreatedAt,
                HomestayId = booking.HomestayId,
                HomestayName = booking.Homestay.Name,
                HomestayCity = booking.Homestay.City,
                HomestayImage = booking.Homestay.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl,
                UserId = booking.UserId,
                UserName = booking.User.FullName,
                UserEmail = booking.User.Email!
            };
        }

        public async Task<List<DateTime>> GetBookedDatesAsync(int homestayId)
        {
            var bookings = await _context.Bookings
                .Where(b => b.HomestayId == homestayId && 
                       (b.Status == BookingStatus.Pending || 
                        b.Status == BookingStatus.Paid ||
                        b.Status == BookingStatus.Completed))
                .ToListAsync();
            
            var bookedDates = new List<DateTime>();
            foreach (var booking in bookings)
            {
                for (var date = booking.CheckInDate; date < booking.CheckOutDate; date = date.AddDays(1))
                {
                    bookedDates.Add(date.Date);
                }
            }
            
            // Add blocked dates
            var blockedDates = await _context.BlockedDates
                .Where(bd => bd.HomestayId == homestayId)
                .Select(bd => bd.Date.Date)
                .ToListAsync();
            
            bookedDates.AddRange(blockedDates);
            return bookedDates.Distinct().OrderBy(d => d).ToList();
        }

        public async Task<AmountCalculationDto> CalculateBookingAmountAsync(
            int homestayId, 
            DateTime checkIn, 
            DateTime checkOut, 
            string? promotionCode = null)
        {
            var homestay = await _context.Homestays.FindAsync(homestayId);
            if (homestay == null) 
                return new AmountCalculationDto();
            
            var nights = (checkOut - checkIn).Days;
            var subtotal = homestay.PricePerNight * nights;
            
            Promotion? promotion = null;
            AmountCalculationDto calc = new AmountCalculationDto
            {
                Subtotal = subtotal,
                Discount = 0,
                Total = subtotal,
                Nights = nights,
                PricePerNight = homestay.PricePerNight,
                PromotionApplied = false,
                PromotionCode = null
            };

            if (!string.IsNullOrEmpty(promotionCode))
            {
                promotion = await _promotionService.GetEntityByCodeIfApplicableAsync(promotionCode, subtotal);
                if (promotion != null)
                {
                    var dto = await _promotionService.CalculateAmountWithPromotionAsync(promotion, subtotal);
                    dto.Nights = nights;
                    dto.PricePerNight = homestay.PricePerNight;
                    return dto;
                }
            }

            return calc;
        }
    }
}
