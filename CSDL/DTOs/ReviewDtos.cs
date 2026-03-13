using System.ComponentModel.DataAnnotations;

namespace Nhom1.DTOs
{
    public class ReviewDto
    {
        public int BookingId { get; set; }
        public int HomestayId { get; set; }
        public string HomestayName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? UserAvatar { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? HostResponse { get; set; }
        public DateTime? HostResponseDate { get; set; }
    }

    public class CreateHostResponseDto
    {
        [Required]
        [StringLength(1000)]
        public string Response { get; set; } = string.Empty;
    }

    public class CreateReviewDto
    {
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }
    }

    public class ReplyToReviewDto
    {
        [Required]
        [StringLength(1000)]
        public string Reply { get; set; } = string.Empty;
    }
}
