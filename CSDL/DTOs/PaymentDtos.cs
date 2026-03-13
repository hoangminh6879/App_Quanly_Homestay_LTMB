using System.ComponentModel.DataAnnotations;

namespace Nhom1.DTOs
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreatePaymentDto
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public string PaymentMethod { get; set; } = "VNPay"; // VNPay, PayPal, CreditCard
    }

    public class PaymentCallbackDto
    {
        public string? TransactionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
    }
}
