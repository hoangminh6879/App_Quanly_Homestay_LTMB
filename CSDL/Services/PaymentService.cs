using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IPaymentService
    {
        Task<PaymentDto?> CreatePaymentAsync(int bookingId, string userId, string paymentMethod);
        Task<PaymentDto?> GetPaymentByBookingAsync(int bookingId);
        Task<bool> UpdatePaymentStatusAsync(string transactionId, PaymentStatus status);
        Task<bool> UpdatePaymentStatusByBookingAsync(int bookingId, string? transactionId, PaymentStatus status);
    }

    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPromotionService _promotionService;

        public PaymentService(ApplicationDbContext context, IPromotionService promotionService)
        {
            _context = context;
            _promotionService = promotionService;
        }

        public async Task<PaymentDto?> CreatePaymentAsync(int bookingId, string userId, string paymentMethod)
        {
            var booking = await _context.Bookings
                .Include(b => b.Homestay)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null)
                return null;

            // Check if payment already exists
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == bookingId);

            if (existingPayment != null)
                return MapToDto(existingPayment);

            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                PaymentMethod = Enum.Parse<PaymentMethod>(paymentMethod, true),
                Status = PaymentStatus.Pending,
                TransactionId = GenerateTransactionId(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return MapToDto(payment);
        }

        public async Task<PaymentDto?> GetPaymentByBookingAsync(int bookingId)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == bookingId);

            return payment == null ? null : MapToDto(payment);
        }

        public async Task<bool> UpdatePaymentStatusAsync(string transactionId, PaymentStatus status)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.TransactionId == transactionId);

            if (payment == null)
                return false;

            payment.Status = status;
            payment.UpdatedAt = DateTime.UtcNow;

            // Update booking status
            if (status == PaymentStatus.Completed)
            {
                var booking = await _context.Bookings.FindAsync(payment.BookingId);
                if (booking != null)
                {
                    booking.Status = BookingStatus.Paid; // Payment completed, set to Paid
                    payment.CompletedAt = DateTime.UtcNow;
                    // If booking had a promotion applied, increment its UsedCount now (policy B)
                    if (booking.PromotionId.HasValue)
                    {
                        try
                        {
                            await _promotionService.IncrementUsageAsync(booking.PromotionId.Value);
                        }
                        catch
                        {
                            // swallow here; promotion usage increment is best-effort but should not block payment update
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdatePaymentStatusByBookingAsync(int bookingId, string? transactionId, PaymentStatus status)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == bookingId);

            if (payment == null)
                return false;

            payment.Status = status;
            payment.UpdatedAt = DateTime.UtcNow;
            
            // Update transactionId if provided (from PayPal capture)
            if (!string.IsNullOrEmpty(transactionId))
            {
                payment.TransactionId = transactionId;
            }

            // Update booking status
            if (status == PaymentStatus.Completed)
            {
                var booking = await _context.Bookings.FindAsync(payment.BookingId);
                if (booking != null)
                {
                    booking.Status = BookingStatus.Paid; // Payment completed, set to Paid
                    payment.CompletedAt = DateTime.UtcNow;
                    // If booking had a promotion applied, increment its UsedCount now (policy B)
                    if (booking.PromotionId.HasValue)
                    {
                        try
                        {
                            await _promotionService.IncrementUsageAsync(booking.PromotionId.Value);
                        }
                        catch
                        {
                            // swallow here; do not fail payment update because of promotion increment
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        private PaymentDto MapToDto(Payment payment)
        {
            return new PaymentDto
            {
                Id = payment.Id,
                BookingId = payment.BookingId,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod.ToString(),
                PaymentStatus = payment.Status.ToString(),
                TransactionId = payment.TransactionId,
                CreatedAt = payment.CreatedAt,
                UpdatedAt = payment.UpdatedAt
            };
        }

        private string GenerateTransactionId()
        {
            return $"TXN{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        }
    }
}
