using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Nhom1.Configurations;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IHomestayService
    {
        Task<PagedResponse<HomestayDto>> GetHomestaysAsync(HomestaySearchDto searchDto);
        Task<HomestayDto?> GetHomestayByIdAsync(int id);
        Task<HomestayDto?> CreateHomestayAsync(CreateHomestayDto createDto, string hostId);
        Task<HomestayDto?> UpdateHomestayAsync(int id, UpdateHomestayDto updateDto, string userId);
        Task<bool> DeleteHomestayAsync(int id, string userId);
        Task<List<HomestayDto>> GetUserHomestaysAsync(string userId);
        Task<int> GetHomestaysCountByHostAsync(string hostId);
        Task<bool> UpdateHomestayStatusAsync(int id, bool isActive, string userId);
        Task<List<string>?> UploadHomestayImagesAsync(int homestayId, List<IFormFile> images, string userId);
        Task<bool> SetPrimaryImageAsync(int homestayId, int imageId, string userId);
        Task<bool> DeleteHomestayImageAsync(int homestayId, int imageId, string userId);
    }

    public class HomestayService : IHomestayService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public HomestayService(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<PagedResponse<HomestayDto>> GetHomestaysAsync(HomestaySearchDto searchDto)
        {
            var query = _context.Homestays
                .Include(h => h.Host)
                .Include(h => h.Images)
                .Include(h => h.HomestayAmenities)
                    .ThenInclude(ha => ha.Amenity)
                .Include(h => h.Bookings)
                .Where(h => h.IsActive && h.IsApproved)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.City))
                query = query.Where(h => h.City.Contains(searchDto.City));

            if (searchDto.Guests.HasValue)
                query = query.Where(h => h.MaxGuests >= searchDto.Guests.Value);

            if (searchDto.MinPrice.HasValue)
                query = query.Where(h => h.PricePerNight >= searchDto.MinPrice.Value);

            if (searchDto.MaxPrice.HasValue)
                query = query.Where(h => h.PricePerNight <= searchDto.MaxPrice.Value);

            if (searchDto.AmenityIds != null && searchDto.AmenityIds.Any())
            {
                query = query.Where(h => h.HomestayAmenities
                    .Any(ha => searchDto.AmenityIds.Contains(ha.AmenityId)));
            }

            // Check availability
            if (searchDto.CheckIn.HasValue && searchDto.CheckOut.HasValue)
            {
                // Filter out homestays with conflicting bookings
                query = query.Where(h => !h.Bookings.Any(b =>
                    b.Status != BookingStatus.Cancelled &&
                    b.CheckInDate < searchDto.CheckOut.Value &&
                    b.CheckOutDate > searchDto.CheckIn.Value));
                
                // Filter out homestays with blocked dates
                query = query.Where(h => !h.BlockedDates.Any(bd =>
                    bd.Date >= searchDto.CheckIn.Value &&
                    bd.Date < searchDto.CheckOut.Value));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)searchDto.PageSize);

            var homestays = await query
                .OrderByDescending(h => h.CreatedAt)
                .Skip((searchDto.Page - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .ToListAsync();
            
            // Map to DTOs in memory (not in database query)
            var homestayDtos = homestays.Select(h => MapToDto(h)).ToList();

            return new PagedResponse<HomestayDto>
            {
                Items = homestayDtos,
                TotalCount = totalCount,
                Page = searchDto.Page,
                PageSize = searchDto.PageSize,
                TotalPages = totalPages
            };
        }

        public async Task<HomestayDto?> GetHomestayByIdAsync(int id)
        {
            var homestay = await _context.Homestays
                .Include(h => h.Host)
                .Include(h => h.Images)
                .Include(h => h.HomestayAmenities)
                    .ThenInclude(ha => ha.Amenity)
                .Include(h => h.Bookings)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (homestay == null)
                return null;

            // Increment view count
            homestay.ViewCount++;
            await _context.SaveChangesAsync();

            return MapToDto(homestay);
        }

        public async Task<HomestayDto?> CreateHomestayAsync(CreateHomestayDto createDto, string hostId)
        {
            var autoApprove = _config.GetValue<bool>("FeatureFlags:AutoApproveHomestays");

            var homestay = new Homestay
            {
                Name = createDto.Name,
                Description = createDto.Description,
                Address = createDto.Address,
                Ward = createDto.Ward,
                District = createDto.District,
                City = createDto.City,
                State = createDto.State,
                Country = createDto.Country,
                ZipCode = createDto.ZipCode,
                Latitude = createDto.Latitude,
                Longitude = createDto.Longitude,
                PricePerNight = createDto.PricePerNight,
                MaxGuests = createDto.MaxGuests,
                Bedrooms = createDto.Bedrooms,
                Bathrooms = createDto.Bathrooms,
                Rules = createDto.Rules,
                YouTubeVideoId = createDto.YouTubeVideoId,
                HostId = hostId,
                IsActive = true,
                IsApproved = autoApprove, // May be auto-approved based on feature flag
                CreatedAt = DateTime.UtcNow
            };

            _context.Homestays.Add(homestay);
            await _context.SaveChangesAsync();

            // Add images
            if (createDto.ImageUrls.Any())
            {
                for (int i = 0; i < createDto.ImageUrls.Count; i++)
                {
                    var image = new HomestayImage
                    {
                        HomestayId = homestay.Id,
                        ImageUrl = createDto.ImageUrls[i],
                        IsPrimary = i == 0,
                        Order = i,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.HomestayImages.Add(image);
                }
            }

            // Add amenities
            if (createDto.AmenityIds.Any())
            {
                foreach (var amenityId in createDto.AmenityIds)
                {
                    _context.HomestayAmenities.Add(new HomestayAmenity
                    {
                        HomestayId = homestay.Id,
                        AmenityId = amenityId
                    });
                }
            }

            await _context.SaveChangesAsync();

            return await GetHomestayByIdAsync(homestay.Id);
        }

        public async Task<HomestayDto?> UpdateHomestayAsync(int id, UpdateHomestayDto updateDto, string userId)
        {
            var homestay = await _context.Homestays
                .FirstOrDefaultAsync(h => h.Id == id && h.HostId == userId);

            if (homestay == null)
                return null;

            if (updateDto.Name != null) homestay.Name = updateDto.Name;
            if (updateDto.Description != null) homestay.Description = updateDto.Description;
            if (updateDto.PricePerNight.HasValue) homestay.PricePerNight = updateDto.PricePerNight.Value;
            if (updateDto.MaxGuests.HasValue) homestay.MaxGuests = updateDto.MaxGuests.Value;
            if (updateDto.Bedrooms.HasValue) homestay.Bedrooms = updateDto.Bedrooms.Value;
            if (updateDto.Bathrooms.HasValue) homestay.Bathrooms = updateDto.Bathrooms.Value;
            if (updateDto.Rules != null) homestay.Rules = updateDto.Rules;
            if (updateDto.YouTubeVideoId != null) homestay.YouTubeVideoId = updateDto.YouTubeVideoId;
            if (updateDto.IsActive.HasValue) homestay.IsActive = updateDto.IsActive.Value;

            homestay.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetHomestayByIdAsync(id);
        }

        public async Task<bool> DeleteHomestayAsync(int id, string userId)
        {
            var homestay = await _context.Homestays
                .FirstOrDefaultAsync(h => h.Id == id && h.HostId == userId);

            if (homestay == null)
                return false;

            // Soft delete
            homestay.IsActive = false;
            homestay.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<HomestayDto>> GetUserHomestaysAsync(string userId)
        {
            var homestays = await _context.Homestays
                .Include(h => h.Host)
                .Include(h => h.Images)
                .Include(h => h.HomestayAmenities)
                    .ThenInclude(ha => ha.Amenity)
                .Where(h => h.HostId == userId)
                .ToListAsync();

            return homestays.Select(h => new HomestayDto
            {
                Id = h.Id,
                Name = h.Name,
                Description = h.Description,
                Address = h.Address,
                City = h.City,
                PricePerNight = h.PricePerNight,
                MaxGuests = h.MaxGuests,
                Bedrooms = h.Bedrooms,
                Bathrooms = h.Bathrooms,
                IsActive = h.IsActive,
                IsApproved = h.IsApproved,
                CreatedAt = h.CreatedAt,
                HostId = h.HostId,
                HostName = h.Host.FullName,
                Images = h.Images.Select(img => new HomestayImageDto
                {
                    Id = img.Id,
                    ImageUrl = GetImageUrl(img.ImageUrl),
                    IsPrimary = img.IsPrimary,
                    DisplayOrder = img.Order
                }).ToList(),
                Amenities = h.HomestayAmenities.Select(ha => new AmenityDto
                {
                    Id = ha.Amenity.Id,
                    Name = ha.Amenity.Name,
                    Icon = ha.Amenity.Icon,
                    Description = ha.Amenity.Description
                }).ToList()
            }).ToList();
        }

        public async Task<int> GetHomestaysCountByHostAsync(string hostId)
        {
            return await _context.Homestays
                .Where(h => h.HostId == hostId)
                .CountAsync();
        }

        public async Task<bool> UpdateHomestayStatusAsync(int id, bool isActive, string userId)
        {
            var homestay = await _context.Homestays.FindAsync(id);
            if (homestay == null || homestay.HostId != userId)
                return false;

            homestay.IsActive = isActive;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<string>?> UploadHomestayImagesAsync(int homestayId, List<IFormFile> images, string userId)
        {
            var homestay = await _context.Homestays
                .Include(h => h.Images)
                .FirstOrDefaultAsync(h => h.Id == homestayId);

            if (homestay == null || homestay.HostId != userId)
                return null;

            var uploadedUrls = new List<string>();
            var currentMaxOrder = homestay.Images.Any() ? homestay.Images.Max(i => i.Order) : 0;

            foreach (var image in images)
            {
                if (image.Length > 0)
                {
                    // Use the upload service to save the file
                    var uploadPath = Path.Combine("homestays", homestayId.ToString());
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                    var fullPath = Path.Combine("wwwroot", "uploads", uploadPath);

                    // Create directory if it doesn't exist
                    Directory.CreateDirectory(fullPath);

                    var filePath = Path.Combine(fullPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    // Relative path for database
                    var relativeUrl = $"/uploads/{uploadPath}/{fileName}";

                    // Create HomestayImage record
                    var homestayImage = new HomestayImage
                    {
                        HomestayId = homestayId,
                        ImageUrl = relativeUrl,
                        Order = ++currentMaxOrder,
                        IsPrimary = !homestay.Images.Any() && uploadedUrls.Count == 0
                    };

                    _context.HomestayImages.Add(homestayImage);
                    uploadedUrls.Add(relativeUrl);
                }
            }

            await _context.SaveChangesAsync();
            return uploadedUrls;
        }

        public async Task<bool> SetPrimaryImageAsync(int homestayId, int imageId, string userId)
        {
            var homestay = await _context.Homestays
                .Include(h => h.Images)
                .FirstOrDefaultAsync(h => h.Id == homestayId);

            if (homestay == null || homestay.HostId != userId)
                return false;

            var targetImage = homestay.Images.FirstOrDefault(i => i.Id == imageId);
            if (targetImage == null)
                return false;

            // Reset all images to non-primary
            foreach (var image in homestay.Images)
            {
                image.IsPrimary = false;
            }

            // Set the target image as primary
            targetImage.IsPrimary = true;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteHomestayImageAsync(int homestayId, int imageId, string userId)
        {
            var homestay = await _context.Homestays
                .Include(h => h.Images)
                .FirstOrDefaultAsync(h => h.Id == homestayId);

            if (homestay == null || homestay.HostId != userId)
                return false;

            var image = homestay.Images.FirstOrDefault(i => i.Id == imageId);
            if (image == null)
                return false;

            // Delete the physical file
            var physicalPath = Path.Combine("wwwroot", image.ImageUrl.TrimStart('/'));
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }

            // If this was the primary image, set another image as primary
            bool wasPrimary = image.IsPrimary;
            _context.HomestayImages.Remove(image);

            if (wasPrimary && homestay.Images.Count > 1)
            {
                var newPrimary = homestay.Images
                    .Where(i => i.Id != imageId)
                    .OrderBy(i => i.Order)
                    .FirstOrDefault();
                if (newPrimary != null)
                {
                    newPrimary.IsPrimary = true;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        private HomestayDto MapToDto(Homestay homestay)
        {
            // Get images and ensure they have proper URLs
            var imagesList = homestay.Images
                .OrderBy(i => i.Order)
                .Select(i => new HomestayImageDto
                {
                    Id = i.Id,
                    ImageUrl = GetImageUrl(i.ImageUrl),
                    IsPrimary = i.IsPrimary,
                    DisplayOrder = i.Order
                })
                .ToList();

            // If no images, add default placeholder
            if (!imagesList.Any())
            {
                imagesList.Add(new HomestayImageDto
                {
                    Id = 0,
                    ImageUrl = ImagePaths.PlaceholderHomestay,
                    IsPrimary = true,
                    DisplayOrder = 0
                });
            }

            return new HomestayDto
            {
                Id = homestay.Id,
                Name = homestay.Name,
                Description = homestay.Description,
                Address = homestay.Address,
                Ward = homestay.Ward,
                District = homestay.District,
                City = homestay.City,
                State = homestay.State,
                Country = homestay.Country,
                ZipCode = homestay.ZipCode,
                Latitude = homestay.Latitude,
                Longitude = homestay.Longitude,
                PricePerNight = homestay.PricePerNight,
                MaxGuests = homestay.MaxGuests,
                Bedrooms = homestay.Bedrooms,
                Bathrooms = homestay.Bathrooms,
                Rules = homestay.Rules,
                YouTubeVideoId = homestay.YouTubeVideoId,
                IsActive = homestay.IsActive,
                IsApproved = homestay.IsApproved,
                ViewCount = homestay.ViewCount,
                AverageRating = homestay.AverageRating,
                ReviewCount = homestay.ReviewCount,
                CreatedAt = homestay.CreatedAt,
                HostId = homestay.HostId,
                HostName = homestay.Host.FullName,
                Images = imagesList,
                Amenities = homestay.HomestayAmenities.Select(ha => new AmenityDto
                {
                    Id = ha.Amenity.Id,
                    Name = ha.Amenity.Name,
                    Icon = ha.Amenity.Icon,
                    Description = ha.Amenity.Description
                }).ToList()
            };
        }

        /// <summary>
        /// Get properly formatted image URL using ImageHelper
        /// </summary>
        private string GetImageUrl(string? imageUrl)
        {
            return ImageHelper.GetHomestayImageUrl(imageUrl);
        }
    }
}
