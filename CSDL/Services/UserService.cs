using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nhom1.Configurations;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IUserService
    {
        Task<UserProfileDto?> GetUserProfileAsync(string userId);
        Task<UserProfileDto?> UpdateProfileAsync(string userId, UpdateProfileDto updateDto);
        Task<string?> UpdateAvatarAsync(string userId, string imageBase64);
        Task<bool> ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto);
    }

    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _environment;

        public UserService(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            return new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                ProfilePicture = ImageHelper.GetUserAvatarUrl(user.ProfilePicture),
                Address = user.Address,
                IsActive = user.IsActive,
                // Populate roles from Identity
                Roles = (await _userManager.GetRolesAsync(user)).ToList(),
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserProfileDto?> UpdateProfileAsync(string userId, UpdateProfileDto updateDto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            if (!string.IsNullOrEmpty(updateDto.FirstName))
                user.FirstName = updateDto.FirstName;

            if (!string.IsNullOrEmpty(updateDto.LastName))
                user.LastName = updateDto.LastName;

            if (!string.IsNullOrEmpty(updateDto.PhoneNumber))
                user.PhoneNumber = updateDto.PhoneNumber;

            if (updateDto.Bio != null)
                user.Bio = updateDto.Bio;

            if (updateDto.Address != null)
                user.Address = updateDto.Address;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) return null;

            return await GetUserProfileAsync(userId);
        }

        public async Task<string?> UpdateAvatarAsync(string userId, string imageBase64)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            try
            {
                // Remove data:image prefix if exists
                var base64Data = imageBase64;
                if (imageBase64.Contains(","))
                {
                    base64Data = imageBase64.Split(',')[1];
                }

                var imageBytes = Convert.FromBase64String(base64Data);
                
                // Generate unique filename using ImageHelper
                var originalFileName = $"{userId}_avatar.jpg";
                var fileName = ImageHelper.GenerateUniqueFileName(originalFileName);
                
                // Get physical path using ImageHelper
                var filePath = ImageHelper.GetPhysicalImagePath(_environment.WebRootPath, "avatar", fileName);
                await File.WriteAllBytesAsync(filePath, imageBytes);

                // Get web path using ImageHelper
                var imageUrl = ImageHelper.GetImageWebPath("avatar", fileName);
                user.ProfilePicture = imageUrl;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded) return null;

                return imageUrl;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
            return result.Succeeded;
        }
    }
}
