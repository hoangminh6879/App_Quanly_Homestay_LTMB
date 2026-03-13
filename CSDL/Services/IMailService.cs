using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IMailService
    {
        Task SendBookingConfirmationAsync(Booking booking);
    }
}
