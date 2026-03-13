using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nhom1.Models;
using System.Net;
using System.Net.Mail;

namespace Nhom1.Services
{
    public class SmtpMailService : IMailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpMailService> _logger;

        public SmtpMailService(IConfiguration config, ILogger<SmtpMailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendBookingConfirmationAsync(Booking booking)
        {
            try
            {
                // Prefer EmailSettings section (matches WebHS appsettings.json). Fallback to Smtp keys for compatibility.
                var emailSection = _config.GetSection("EmailSettings");
                var smtpHost = emailSection["SmtpServer"] ?? _config["Smtp:Host"] ?? "smtp.gmail.com";
                var smtpPort = emailSection.GetValue<int?>("SmtpPort") ?? _config.GetValue<int?>("Smtp:Port") ?? 587;
                var smtpUser = emailSection["SmtpUsername"] ?? _config["Smtp:Username"];
                var smtpPass = emailSection["SmtpPassword"] ?? _config["Smtp:Password"]; // Recommend app password or secret
                var fromName = emailSection["SenderName"] ?? _config["Smtp:FromName"] ?? "Homestay App";
                var fromEmail = emailSection["SenderEmail"] ?? _config["Smtp:FromEmail"] ?? smtpUser;
                var useSsl = emailSection.GetValue<bool?>("UseSsl") ?? true;
                var enableSending = emailSection.GetValue<bool?>("EnableEmailSending") ?? true;
                var maxAttempts = emailSection.GetValue<int?>("MaxRetryAttempts") ?? 1;
                var retryDelaySeconds = emailSection.GetValue<int?>("RetryDelaySeconds") ?? 1;

                if (!enableSending)
                {
                    _logger.LogInformation("Email sending disabled by configuration; skipping email for booking {BookingId}", booking.Id);
                    return;
                }

                if (string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPass))
                {
                    _logger.LogWarning("SMTP credentials not configured; skipping email send for booking {BookingId}", booking.Id);
                    return;
                }

                var toEmail = booking.User?.Email;
                if (string.IsNullOrEmpty(toEmail))
                {
                    _logger.LogWarning("Booking {BookingId} has no user email; skipping confirmation email", booking.Id);
                    return;
                }

                // Use local non-null variables so compiler knows values are not null
                var smtpUserNonNull = smtpUser!;
                var smtpPassNonNull = smtpPass!;
                var fromEmailResolved = fromEmail ?? smtpUserNonNull;
                var toEmailResolved = toEmail!;

                var subject = $"Booking Confirmed - #{booking.Id}";
                var body = $@"
Hello {booking.User?.FullName},

Your booking for {booking.Homestay?.Name} is confirmed.

Booking details:
Booking Id: {booking.Id}
Check-in: {booking.CheckInDate:yyyy-MM-dd}
Check-out: {booking.CheckOutDate:yyyy-MM-dd}
Nights: {(booking.NumberOfNights)}
Guests: {booking.NumberOfGuests}
Total: {booking.FinalAmount:C}

Thank you for booking with us.

Best regards,
{fromName}
";

                var mail = new MailMessage()
                {
                    From = new MailAddress(fromEmailResolved, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                mail.To.Add(new MailAddress(toEmailResolved));

                // Attempt send with retries
                var attempt = 0;
                Exception? lastEx = null;
                while (attempt < maxAttempts)
                {
                    attempt++;
                    try
                    {
                        using var client = new SmtpClient(smtpHost, smtpPort)
                        {
                            EnableSsl = useSsl,
                            Credentials = new NetworkCredential(smtpUserNonNull, smtpPassNonNull)
                        };

                        await client.SendMailAsync(mail);
                        _logger.LogInformation("Booking confirmation email sent for booking {BookingId} to {Email} (attempt {Attempt})", booking.Id, toEmailResolved, attempt);
                        lastEx = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        _logger.LogWarning(ex, "Attempt {Attempt} failed sending booking email for booking {BookingId}", attempt, booking.Id);
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                        }
                    }
                }

                if (lastEx != null)
                {
                    _logger.LogError(lastEx, "All attempts failed sending booking confirmation email for booking {BookingId}", booking.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send booking confirmation email for booking {BookingId}", booking.Id);
            }
        }
    }
}
