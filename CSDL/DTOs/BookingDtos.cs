using System.ComponentModel.DataAnnotations;
using Nhom1.Models;

namespace Nhom1.DTOs
{
    public class BookingDto
    {
        public int Id { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public int NumberOfGuests { get; set; }
        public int NumberOfNights { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public BookingStatus Status { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Review
        public int? ReviewRating { get; set; }
        public string? ReviewComment { get; set; }
        public DateTime? ReviewCreatedAt { get; set; }
        
        // Related data
        public int HomestayId { get; set; }
        public string HomestayName { get; set; } = string.Empty;
        public string HomestayCity { get; set; } = string.Empty;
        public string? HomestayImage { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
    }

    public class CreateBookingDto
    {
        [Required]
        public int HomestayId { get; set; }

        [Required]
        public DateTime CheckInDate { get; set; }

        [Required]
        public DateTime CheckOutDate { get; set; }

        [Required]
        [Range(1, 50)]
        public int NumberOfGuests { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
        
        // Optional promotion code provided by the user during booking
        public string? PromotionCode { get; set; }
    }

    public class UpdateBookingStatusDto
    {
        [Required]
        public BookingStatus Status { get; set; }
    }

    public class BookingAvailabilityDto
    {
        [Required]
        public int HomestayId { get; set; }

        [Required]
        public DateTime CheckInDate { get; set; }

        [Required]
        public DateTime CheckOutDate { get; set; }
    }

    public class AvailabilityResponseDto
    {
        public bool IsAvailable { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<DateTime>? UnavailableDates { get; set; }
    }

    public class CalculateAmountDto
    {
        [Required]
        public int HomestayId { get; set; }

        [Required]
        public DateTime CheckIn { get; set; }

        [Required]
        public DateTime CheckOut { get; set; }

        public string? PromotionCode { get; set; }
    }
}
