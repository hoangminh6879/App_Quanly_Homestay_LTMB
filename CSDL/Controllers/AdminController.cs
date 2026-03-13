using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    // NOTE: route intentionally differs from AdminApiController to avoid endpoint collisions
    [Route("api/admin/legacy")]
    [ApiExplorerSettings(IgnoreApi = true)] // hide legacy controller from API discovery
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Get system statistics - Total users, homestays, bookings, revenue
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var totalUsers = await _context.Users.Where(u => u.IsActive).CountAsync();
                var totalHomestays = await _context.Homestays.CountAsync();
                var totalBookings = await _context.Bookings.CountAsync();
                var totalRevenue = await _context.Bookings
                    .Where(b => b.Status == BookingStatus.Paid || b.Status == BookingStatus.Completed)
                    .SumAsync(b => b.FinalAmount);

                var stats = new
                {
                    totalUsers,
                    totalHomestays,
                    totalBookings,
                    totalRevenue,
                    generatedAt = DateTime.UtcNow
                };

                return Ok(ApiResponse<object>.SuccessResponse(stats));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get all users with pagination
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var users = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalCount = await _context.Users.CountAsync();

                // Get roles for each user
                var userDtos = new List<object>();
                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userDtos.Add(new
                    {
                        user.Id,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        user.PhoneNumber,
                        user.IsActive,
                        user.CreatedAt,
                        Roles = roles
                    });
                }

                var response = new
                {
                    items = userDtos,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return Ok(ApiResponse<object>.SuccessResponse(response));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Update user active status
        /// </summary>
        [HttpPut("users/{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(string id, [FromBody] UpdateUserStatusDto dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound(ApiResponse<object>.ErrorResponse("User not found"));

                user.IsActive = dto.IsActive;
                await _userManager.UpdateAsync(user);

                return Ok(ApiResponse<object>.SuccessResponse(null!, "User status updated successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Update user role
        /// </summary>
        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(string id, [FromBody] UpdateUserRoleDto dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound(ApiResponse<object>.ErrorResponse("User not found"));

                // Validate role
                if (dto.Role != UserRoles.Admin && dto.Role != UserRoles.Host && dto.Role != UserRoles.User)
                    return BadRequest(ApiResponse<object>.ErrorResponse("Invalid role"));

                // Remove all existing roles
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

                // Add new role
                await _userManager.AddToRoleAsync(user, dto.Role);

                return Ok(ApiResponse<object>.SuccessResponse(null!, "User role updated successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get all homestays with pagination
        /// </summary>
        [HttpGet("homestays")]
        public async Task<IActionResult> GetAllHomestays([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var homestays = await _context.Homestays
                    .Include(h => h.Host)
                    .Include(h => h.Images)
                    .OrderByDescending(h => h.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(h => new
                    {
                        h.Id,
                        h.Name,
                        h.Description,
                        h.Address,
                        h.City,
                        h.PricePerNight,
                        h.MaxGuests,
                        h.IsApproved,
                        h.CreatedAt,
                        Host = new
                        {
                            h.Host.Id,
                            h.Host.Email,
                            h.Host.FirstName,
                            h.Host.LastName
                        },
                        ThumbnailImage = h.Images.FirstOrDefault(i => i.IsPrimary) != null
                            ? h.Images.FirstOrDefault(i => i.IsPrimary)!.ImageUrl
                            : h.Images.FirstOrDefault() != null
                                ? h.Images.FirstOrDefault()!.ImageUrl
                                : null,
                        TotalBookings = _context.Bookings.Count(b => b.HomestayId == h.Id)
                    })
                    .ToListAsync();

                var totalCount = await _context.Homestays.CountAsync();

                var response = new
                {
                    items = homestays,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return Ok(ApiResponse<object>.SuccessResponse(response));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Update homestay approval status
        /// </summary>
        [HttpPut("homestays/{id}/approve")]
        public async Task<IActionResult> ApproveHomestay(int id, [FromBody] ApproveHomestayDto dto)
        {
            try
            {
                var homestay = await _context.Homestays.FindAsync(id);
                if (homestay == null)
                    return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found"));

                homestay.IsApproved = dto.IsApproved;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.SuccessResponse(null!, "Homestay approval status updated successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get all bookings with pagination
        /// </summary>
        [HttpGet("bookings")]
        public async Task<IActionResult> GetAllBookings([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var bookings = await _context.Bookings
                    .Include(b => b.Homestay)
                    .Include(b => b.User)
                    .OrderByDescending(b => b.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(b => new
                    {
                        b.Id,
                        b.CheckInDate,
                        b.CheckOutDate,
                        b.NumberOfGuests,
                        b.TotalAmount,
                        b.FinalAmount,
                        b.Status,
                        b.CreatedAt,
                        Homestay = new
                        {
                            b.Homestay.Id,
                            b.Homestay.Name,
                            b.Homestay.Address,
                            b.Homestay.City
                        },
                        User = new
                        {
                            b.User.Id,
                            b.User.Email,
                            b.User.FirstName,
                            b.User.LastName
                        }
                    })
                    .ToListAsync();

                var totalCount = await _context.Bookings.CountAsync();

                var response = new
                {
                    items = bookings,
                    totalCount,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };

                return Ok(ApiResponse<object>.SuccessResponse(response));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Delete a user (soft delete by setting IsActive to false)
        /// </summary>
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound(ApiResponse<object>.ErrorResponse("User not found"));

                // Prevent admin from deleting themselves
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (user.Id == currentUserId)
                    return BadRequest(ApiResponse<object>.ErrorResponse("Cannot delete your own account"));

                // Soft delete by setting IsActive to false
                user.IsActive = false;
                await _userManager.UpdateAsync(user);

                return Ok(ApiResponse<object>.SuccessResponse(null!, "User deleted successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Delete a homestay
        /// </summary>
        [HttpDelete("homestays/{id}")]
        public async Task<IActionResult> DeleteHomestay(int id)
        {
            try
            {
                var homestay = await _context.Homestays.FindAsync(id);
                if (homestay == null)
                    return NotFound(ApiResponse<object>.ErrorResponse("Homestay not found"));

                // Check if there are active bookings
                var hasActiveBookings = await _context.Bookings
                    .AnyAsync(b => b.HomestayId == id && 
                        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Paid));

                if (hasActiveBookings)
                    return BadRequest(ApiResponse<object>.ErrorResponse("Cannot delete homestay with active bookings"));

                _context.Homestays.Remove(homestay);
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<object>.SuccessResponse(null!, "Homestay deleted successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<object>.ErrorResponse($"Internal server error: {ex.Message}"));
            }
        }
    }

    // Admin DTOs
    public class UpdateUserStatusDto
    {
        public bool IsActive { get; set; }
    }

    public class UpdateUserRoleDto
    {
        public string Role { get; set; } = string.Empty;
    }

    public class ApproveHomestayDto
    {
        public bool IsApproved { get; set; }
    }
}
