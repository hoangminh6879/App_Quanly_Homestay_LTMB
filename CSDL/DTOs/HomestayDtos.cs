using System.ComponentModel.DataAnnotations;

namespace Nhom1.DTOs
{
    public class HomestayDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal PricePerNight { get; set; }
        public int MaxGuests { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public string? Rules { get; set; }
        public string? YouTubeVideoId { get; set; }
        public bool IsActive { get; set; }
        public bool IsApproved { get; set; }
        public int ViewCount { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string HostId { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public List<HomestayImageDto> Images { get; set; } = new();
        public List<AmenityDto> Amenities { get; set; } = new();
    }

    public class CreateHomestayDto
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(300)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Ward { get; set; }

        [StringLength(100)]
        public string? District { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        [StringLength(100)]
        public string Country { get; set; } = "Vietnam";

        [Required]
        [StringLength(20)]
        public string ZipCode { get; set; } = string.Empty;

        [Required]
        public decimal Latitude { get; set; }

        [Required]
        public decimal Longitude { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal PricePerNight { get; set; }

        [Required]
        [Range(1, 100)]
        public int MaxGuests { get; set; }

        [Required]
        [Range(1, 50)]
        public int Bedrooms { get; set; }

        [Required]
        [Range(1, 50)]
        public int Bathrooms { get; set; }

        [StringLength(2000)]
        public string? Rules { get; set; }

        [StringLength(50)]
        public string? YouTubeVideoId { get; set; }

        public List<int> AmenityIds { get; set; } = new();
        public List<string> ImageUrls { get; set; } = new();
    }

    public class UpdateHomestayDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? PricePerNight { get; set; }

        [Range(1, 100)]
        public int? MaxGuests { get; set; }

        [Range(1, 50)]
        public int? Bedrooms { get; set; }

        [Range(1, 50)]
        public int? Bathrooms { get; set; }

        [StringLength(2000)]
        public string? Rules { get; set; }

        [StringLength(50)]
        public string? YouTubeVideoId { get; set; }

        public bool? IsActive { get; set; }
    }

    public class UpdateHomestayStatusDto
    {
        [Required]
        public bool IsActive { get; set; }
    }

    public class HomestayImageDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class AmenityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Description { get; set; }
    }

    public class HomestaySearchDto
    {
        public string? City { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public int? Guests { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public List<int>? AmenityIds { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
