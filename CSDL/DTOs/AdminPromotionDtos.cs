using System.ComponentModel.DataAnnotations;
using Nhom1.Models;

namespace Nhom1.DTOs
{
    public class CreatePromotionDto
    {
        [Required, StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public PromotionType Type { get; set; }

        [Required]
        [Range(0.0, double.MaxValue)]
        public decimal Value { get; set; }

        [Range(0.0, double.MaxValue)]
        public decimal? MinOrderAmount { get; set; }

        [Range(0.0, double.MaxValue)]
        public decimal? MaxDiscountAmount { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public int? UsageLimit { get; set; }
    }

    public class UpdatePromotionDto : CreatePromotionDto
    {
        [Required]
        public int Id { get; set; }
    }
}
