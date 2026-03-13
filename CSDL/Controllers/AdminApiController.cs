using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.Models;
using Nhom1.Services;

namespace Nhom1.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminApiController : ControllerBase
    {
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AdminApiController> _logger;
    private readonly IMailService _mailService;

        public AdminApiController(ApplicationDbContext context, UserManager<User> userManager, ILogger<AdminApiController> logger, IMailService mailService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _mailService = mailService;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalUsers = await _context.Users.CountAsync(u => !string.IsNullOrEmpty(u.Email) && u.IsActive);
            var totalHomestays = await _context.Homestays.CountAsync();
            var totalBookings = await _context.Bookings.CountAsync(b => b.Status != BookingStatus.Cancelled);
            var totalRevenue = await _context.Bookings
                .Where(b => (b.Status == BookingStatus.Paid || b.Status == BookingStatus.Completed) && b.FinalAmount > 0)
                .SumAsync(b => b.FinalAmount);

            var pendingHomestays = await _context.Homestays.CountAsync(h => !h.IsApproved && h.IsActive);

            // Build simple time series for the last 7 days to support frontend charts
            var today = DateTime.UtcNow.Date;
            var days = Enumerable.Range(0, 7).Select(i => today.AddDays(-(6 - i))).ToList(); // oldest..newest

            var since = days.First();

            var grouped = await _context.Bookings
                .Where(b => b.CreatedAt >= since)
                .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month, b.CreatedAt.Day })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Day = g.Key.Day,
                    Count = g.Count(),
                    Revenue = g.Where(b => (b.Status == BookingStatus.Paid || b.Status == BookingStatus.Completed) && b.FinalAmount > 0).Sum(b => (decimal?)b.FinalAmount) ?? 0m
                })
                .ToListAsync();

            // Map grouped results into dictionaries keyed by date string yyyy-MM-dd
            var bookingsByDay = new Dictionary<string, int>();
            var revenueByDay = new Dictionary<string, decimal>();

            // initialize keys with zero to ensure consistent ordering
            foreach (var d in days)
            {
                var k = d.ToString("yyyy-MM-dd");
                bookingsByDay[k] = 0;
                revenueByDay[k] = 0m;
            }

            foreach (var g in grouped)
            {
                var dt = new DateTime(g.Year, g.Month, g.Day);
                var k = dt.ToString("yyyy-MM-dd");
                if (bookingsByDay.ContainsKey(k))
                {
                    bookingsByDay[k] = g.Count;
                    revenueByDay[k] = g.Revenue;
                }
            }

            // Prepare revenueTrend as array of numbers (matching day order)
            var revenueTrend = days.Select(d => (double)revenueByDay[d.ToString("yyyy-MM-dd")] ).ToList();

            return Ok(new
            {
                totalUsers,
                totalHomestays,
                totalBookings,
                totalRevenue,
                pendingHomestays,
                // Frontend expects a list for revenueTrend and a map for bookingsByDay
                revenueTrend,
                bookingsByDay,
                generatedAt = DateTime.UtcNow
            });
        }

        [HttpGet("homestays")]
        public async Task<IActionResult> GetHomestays(int page = 1, int pageSize = 20, string status = "", string q = "")
        {
            var query = _context.Homestays
                .Include(h => h.Host)
                .Include(h => h.Images)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                switch (status.ToLower())
                {
                    case "pending":
                        query = query.Where(h => !h.IsApproved && h.IsActive);
                        break;
                    case "approved":
                        query = query.Where(h => h.IsApproved && h.IsActive);
                        break;
                    case "inactive":
                        query = query.Where(h => !h.IsActive);
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                query = query.Where(h => (h.Name != null && h.Name.ToLower().Contains(qLower))
                    || (h.Address != null && h.Address.ToLower().Contains(qLower))
                    || (h.City != null && h.City.ToLower().Contains(qLower))
                    || (h.Host != null && h.Host.Email != null && h.Host.Email.ToLower().Contains(qLower)));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(h => h.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(h => new
                {
                    id = h.Id,
                    name = h.Name,
                    address = h.Address,
                    city = h.City,
                    isApproved = h.IsApproved,
                    isActive = h.IsActive,
                    hostEmail = h.Host != null ? h.Host.Email : string.Empty,
                    createdAt = h.CreatedAt,
                    // thumbnail: use primary image if available (safe projection)
                    thumbnail = h.Images.OrderBy(i => i.Order).Select(i => i.ImageUrl).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new { items, total, page, pageSize });
        }

        [HttpPut("homestays/bulk-approve")]
        public async Task<IActionResult> BulkApproveHomestays([FromBody] BulkApproveRequest req)
        {
            if (req?.Ids == null || !req.Ids.Any()) return BadRequest(new { error = "No ids provided" });

            var homestays = await _context.Homestays.Where(h => req.Ids.Contains(h.Id)).ToListAsync();
            foreach (var h in homestays)
            {
                h.IsApproved = req.IsApproved;
                if (req.IsApproved) h.IsActive = true;
                h.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await LogActionAsync("BulkHomestayApprove", $"Ids={string.Join(',', req.Ids)} approved={req.IsApproved}");
            return Ok(new { success = true, count = homestays.Count });
        }

        [HttpPut("homestays/{id}/approve")]
        public async Task<IActionResult> ApproveHomestay(int id, [FromBody] ApproveRequest req)
        {
            var homestay = await _context.Homestays.Include(h => h.Host).FirstOrDefaultAsync(h => h.Id == id);
            if (homestay == null) return NotFound();

            homestay.IsApproved = req.IsApproved;
            if (req.IsApproved) homestay.IsActive = true;
            homestay.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Homestay {Id} approved={Approved} by {User}", id, req.IsApproved, User?.Identity?.Name ?? "system");

            return Ok(new { success = true });
        }

        [HttpPost("homestays/{id}/reject")]
        public async Task<IActionResult> RejectHomestay(int id)
        {
            var homestay = await _context.Homestays.Include(h => h.Host).FirstOrDefaultAsync(h => h.Id == id);
            if (homestay == null) return NotFound();

            homestay.IsApproved = false;
            homestay.IsActive = false;
            homestay.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Homestay {Id} rejected by {User}", id, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        [HttpPut("homestays/{id}/activate")]
        public async Task<IActionResult> ActivateHomestay(int id)
        {
            var homestay = await _context.Homestays.FindAsync(id);
            if (homestay == null) return NotFound();

            homestay.IsActive = true;
            homestay.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Homestay {Id} activated by {User}", id, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        [HttpPut("homestays/{id}/deactivate")]
        public async Task<IActionResult> DeactivateHomestay(int id)
        {
            var homestay = await _context.Homestays.FindAsync(id);
            if (homestay == null) return NotFound();

            homestay.IsActive = false;
            homestay.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Homestay {Id} deactivated by {User}", id, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        [HttpDelete("homestays/{id}")]
        public async Task<IActionResult> DeleteHomestay(int id)
        {
            var homestay = await _context.Homestays.FindAsync(id);
            if (homestay == null) return NotFound();

            homestay.IsActive = false;
            homestay.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Homestay {Id} soft-deleted by {User}", id, User?.Identity?.Name ?? "system");

            return Ok(new { success = true });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(int page = 1, int pageSize = 20, string q = "")
        {
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                query = query.Where(u => (u.Email != null && u.Email.ToLower().Contains(qLower)) || (u.FullName != null && u.FullName.ToLower().Contains(qLower)));
            }

            var total = await query.CountAsync();

            // materialize the page of users, then fetch roles per user (keeps response small and simple)
            var users = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = new List<object>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                items.Add(new
                {
                    id = u.Id,
                    email = u.Email,
                    fullName = u.FullName,
                    isActive = u.IsActive,
                    createdAt = u.CreatedAt,
                    roles = roles // list of role names
                });
            }

            return Ok(new { items, total, page, pageSize });
        }

        [HttpPut("users/{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(string id, [FromBody] UserStatusRequest req)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = req.IsActive;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("User {Id} status set isActive={IsActive} by {User}", id, req.IsActive, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(string id, [FromBody] UserRoleRequest req)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var r in roles) await _userManager.RemoveFromRoleAsync(user, r);
            if (!string.IsNullOrEmpty(req.Role)) await _userManager.AddToRoleAsync(user, req.Role);

            _logger.LogInformation("User {Id} role set to {Role} by {User}", id, req.Role, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded) return BadRequest(new { success = false, errors = result.Errors });

            _logger.LogInformation("User {Id} deleted by {User}", id, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        [HttpGet("bookings")]
        public async Task<IActionResult> GetBookings(int page = 1, int pageSize = 20, string status = "all", string q = "")
        {
            var query = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Homestay).ThenInclude(h => h.Host)
                .AsQueryable();

            if (status != "all" && Enum.TryParse<BookingStatus>(status, true, out var s))
            {
                query = query.Where(b => b.Status == s);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                // search by booking id, homestay name or user name/email
                if (int.TryParse(q, out var id))
                {
                    query = query.Where(b => b.Id == id || b.Homestay.Name.ToLower().Contains(qLower) || (b.User.Email != null && b.User.Email.ToLower().Contains(qLower)));
                }
                else
                {
                    query = query.Where(b => (b.Homestay.Name != null && b.Homestay.Name.ToLower().Contains(qLower)) || (b.User.Email != null && b.User.Email.ToLower().Contains(qLower)) || ((b.User.FirstName + " " + b.User.LastName).ToLower().Contains(qLower)));
                }
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    id = b.Id,
                    homestayName = b.Homestay.Name,
                    userName = b.User.FirstName + " " + b.User.LastName,
                    checkIn = b.CheckInDate,
                    checkOut = b.CheckOutDate,
                    finalAmount = b.FinalAmount,
                    status = b.Status
                })
                .ToListAsync();

            return Ok(new { items, total, page, pageSize });
        }

        [HttpPut("bookings/{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] BookingStatusRequest req)
        {
            var booking = await _context.Bookings.Include(b => b.Homestay).FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null) return NotFound();

            if (!Enum.TryParse<BookingStatus>(req.Status, true, out var s)) return BadRequest(new { error = "Invalid status" });

            booking.Status = s;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // If booking was moved to Paid, create blocked dates
            if (s == BookingStatus.Paid)
            {
                var blockedDates = new List<BlockedDate>();
                var currentDate = booking.CheckInDate.Date;
                while (currentDate < booking.CheckOutDate.Date)
                {
                    blockedDates.Add(new BlockedDate
                    {
                        HomestayId = booking.HomestayId,
                        Date = currentDate,
                        Reason = $"Booking #{booking.Id} (admin confirmed)",
                        CreatedAt = DateTime.UtcNow
                    });
                    currentDate = currentDate.AddDays(1);
                }

                if (blockedDates.Any())
                {
                    _context.BlockedDates.AddRange(blockedDates);
                    await _context.SaveChangesAsync();
                }
            }

            _logger.LogInformation("Booking {Id} status set to {Status} by {User}", id, s, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        [HttpPost("bookings/{id}/confirm")]
        public async Task<IActionResult> ConfirmBooking(int id)
        {
            // Set status to Paid if possible, reusing UpdateBookingStatus logic
            var booking = await _context.Bookings.Include(b => b.Homestay).FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null) return NotFound();

            if (booking.Status == BookingStatus.Paid)
            {
                return Ok(new { success = true, message = "Booking already paid" });
            }

            booking.Status = BookingStatus.Paid;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // create blocked dates
            var blockedDates = new List<BlockedDate>();
            var currentDate = booking.CheckInDate.Date;
            while (currentDate < booking.CheckOutDate.Date)
            {
                blockedDates.Add(new BlockedDate
                {
                    HomestayId = booking.HomestayId,
                    Date = currentDate,
                    Reason = $"Booking #{booking.Id} (admin confirmed)",
                    CreatedAt = DateTime.UtcNow
                });
                currentDate = currentDate.AddDays(1);
            }

            if (blockedDates.Any())
            {
                _context.BlockedDates.AddRange(blockedDates);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Booking {Id} confirmed (Paid) by {User}", id, User?.Identity?.Name ?? "system");

            // Send booking confirmation email (fire-and-forget)
            try
            {
                // reload booking with related user/homestay to ensure email and names available
                var bookingWithDetails = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.Homestay)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (bookingWithDetails != null)
                {
                    _ = _mailService.SendBookingConfirmationAsync(bookingWithDetails);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue/send booking confirmation email for booking {BookingId}", id);
            }

            return Ok(new { success = true });
        }

        [HttpPost("bookings/{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null) return NotFound();

            booking.Status = BookingStatus.Cancelled;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Booking {Id} cancelled by {User}", id, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        [HttpPost("bookings/{id}/complete")]
        public async Task<IActionResult> CompleteBooking(int id)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null) return NotFound();

            booking.Status = BookingStatus.Completed;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Booking {Id} completed by {User}", id, User?.Identity?.Name ?? "system");
            return Ok(new { success = true });
        }

        // Internal helper to log admin actions; kept simple and non-persistent (ILogger only)
        private Task LogActionAsync(string action, string details)
        {
            _logger.LogInformation("AdminAction: {Action} - {Details} by {User}", action, details, User?.Identity?.Name ?? "system");
            return Task.CompletedTask;
        }

        public class BookingStatusRequest { public string? Status { get; set; } }

        public class ApproveRequest { public bool IsApproved { get; set; } }
    public class BulkApproveRequest { public List<int> Ids { get; set; } = new(); public bool IsApproved { get; set; } }
        public class UserStatusRequest { public bool IsActive { get; set; } }
        public class UserRoleRequest { public string? Role { get; set; } }
    }
}
