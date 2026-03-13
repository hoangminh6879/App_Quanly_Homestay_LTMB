using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nhom1.DTOs;
using Nhom1.Services;
using System.Security.Claims;

namespace Nhom1.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var profile = await _userService.GetUserProfileAsync(userId);
            if (profile == null)
                return NotFound(ApiResponse<object>.ErrorResponse("User not found"));

            return Ok(ApiResponse<UserProfileDto>.SuccessResponse(profile));
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto updateDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var profile = await _userService.UpdateProfileAsync(userId, updateDto);
            if (profile == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to update profile"));

            return Ok(ApiResponse<UserProfileDto>.SuccessResponse(profile, "Profile updated successfully"));
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _userService.ChangePasswordAsync(userId, changePasswordDto);
            if (!success)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to change password. Please check your current password."));

            return Ok(ApiResponse<object>.SuccessResponse(null!, "Password changed successfully"));
        }

        [HttpPost("avatar")]
        public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarDto avatarDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var imageUrl = await _userService.UpdateAvatarAsync(userId, avatarDto.ImageBase64);
            if (imageUrl == null)
                return BadRequest(ApiResponse<object>.ErrorResponse("Failed to upload avatar"));

            return Ok(ApiResponse<object>.SuccessResponse(new { avatarUrl = imageUrl }, "Avatar updated successfully"));
        }
    }
}
