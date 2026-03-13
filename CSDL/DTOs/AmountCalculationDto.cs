using System;

namespace Nhom1.DTOs
{
    public class AmountCalculationDto
    {
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public int Nights { get; set; }
        public decimal PricePerNight { get; set; }
        public bool PromotionApplied { get; set; }
        public string? PromotionCode { get; set; }
    }
}
