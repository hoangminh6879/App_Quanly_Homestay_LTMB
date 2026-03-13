using System.ComponentModel.DataAnnotations;

namespace Nhom1.DTOs
{
    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePicture { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        // Roles assigned to the user (e.g., "User", "Host", "Admin")
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateProfileDto
    {
        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }
    }

    public class UpdateAvatarDto
    {
        [Required]
        public string ImageBase64 { get; set; } = string.Empty;
    }
}
