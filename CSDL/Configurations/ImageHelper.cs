namespace Nhom1.Configurations
{
    /// <summary>
    /// Image path constants for the application
    /// Manages all image paths similar to WebHS implementation
    /// </summary>
    public static class ImagePaths
    {
        // Base paths
        public const string ImagesBase = "/images";
        
        // Default/Placeholder Images
        public const string DefaultHomestay = "/images/default-homestay.jpg";
        public const string PlaceholderHomestay = "/images/default-homestay.jpg";
        public const string DefaultAvatar = "/images/default-avatar.jpg";
        public const string DefaultAvatarSvg = "/images/default-avatar.svg";
        public const string NoImage = "/images/no-image.jpg";
        
        // Homestay subdirectories
        public const string HomestaysFolder = "/images/homestays";
        public const string HomestaysBathrooms = "/images/homestays/bathrooms";
        public const string HomestaysBedrooms = "/images/homestays/bedrooms";
        public const string HomestaysCommonAreas = "/images/homestays/common-areas";
        public const string HomestaysExterior = "/images/homestays/exterior";
        
        // User images
        public const string UsersFolder = "/images/users";
        
        // Admin images
        public const string AdminFolder = "/images/admin";
        public const string AdminLogo = "/images/admin/logo.png";
        
        // Tudong (Sample images from WebHS)
        public const string TudongFolder = "/images/Tudong";
        
        // Placeholders
        public const string PlaceholdersFolder = "/images/placeholders";
        public const string PlaceholderNoImage = "/images/placeholders/no-image.jpg";
        
        /// <summary>
        /// Get Tudong sample image by number (1-7)
        /// </summary>
        public static string GetTudongImage(int number)
        {
            if (number < 1 || number > 7)
                number = 1;
            return $"{TudongFolder}/{number}.jpg";
        }
        
        /// <summary>
        /// Get all Tudong sample images
        /// </summary>
        public static List<string> GetAllTudongImages()
        {
            return Enumerable.Range(1, 7)
                .Select(i => GetTudongImage(i))
                .ToList();
        }
    }
    
    /// <summary>
    /// Helper methods for image URL processing
    /// </summary>
    public static class ImageHelper
    {
        /// <summary>
        /// Ensures image URL is properly formatted
        /// - If empty/null: returns default placeholder
        /// - If relative path (starts with /): keeps as is
        /// - If full URL: keeps as is
        /// - Otherwise: prepends /images/
        /// </summary>
        public static string GetImageUrl(string? imageUrl, string? defaultImage = null)
        {
            // If null or empty, return default placeholder
            if (string.IsNullOrWhiteSpace(imageUrl))
                return defaultImage ?? ImagePaths.PlaceholderHomestay;

            // If already a full URL (http/https), return as is
            if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return imageUrl;

            // If already starts with /, return as is
            if (imageUrl.StartsWith("/"))
                return imageUrl;

            // Otherwise, prepend /images/
            return $"{ImagePaths.ImagesBase}/{imageUrl}";
        }
        
        /// <summary>
        /// Get homestay image URL with fallback to default
        /// </summary>
        public static string GetHomestayImageUrl(string? imageUrl)
        {
            return GetImageUrl(imageUrl, ImagePaths.PlaceholderHomestay);
        }
        
        /// <summary>
        /// Get user avatar URL with fallback to default
        /// </summary>
        public static string GetUserAvatarUrl(string? imageUrl)
        {
            return GetImageUrl(imageUrl, ImagePaths.DefaultAvatar);
        }
        
        /// <summary>
        /// Build full image URL with base URL (for Conveyor/external access)
        /// </summary>
        public static string BuildFullImageUrl(string baseUrl, string? imageUrl)
        {
            var processedUrl = GetImageUrl(imageUrl);
            
            // If already full URL, return as is
            if (processedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                processedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return processedUrl;
                
            // Ensure baseUrl doesn't end with /
            baseUrl = baseUrl.TrimEnd('/');
            
            return $"{baseUrl}{processedUrl}";
        }
        
        /// <summary>
        /// Get image path for saving file (physical path)
        /// </summary>
        public static string GetPhysicalImagePath(string wwwrootPath, string category, string fileName)
        {
            var folder = category.ToLower() switch
            {
                "homestay" => "homestays",
                "user" => "users",
                "avatar" => "users",
                "admin" => "admin",
                _ => "uploads"
            };
            
            var fullPath = Path.Combine(wwwrootPath, "images", folder);
            
            // Ensure directory exists
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
                
            return Path.Combine(fullPath, fileName);
        }
        
        /// <summary>
        /// Get image URL for saved file (web path)
        /// </summary>
        public static string GetImageWebPath(string category, string fileName)
        {
            var folder = category.ToLower() switch
            {
                "homestay" => "homestays",
                "user" => "users",
                "avatar" => "users",
                "admin" => "admin",
                _ => "uploads"
            };
            
            return $"{ImagePaths.ImagesBase}/{folder}/{fileName}";
        }
        
        /// <summary>
        /// Validate image file extension
        /// </summary>
        public static bool IsValidImageExtension(string fileName)
        {
            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".svg", ".webp" };
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return validExtensions.Contains(extension);
        }
        
        /// <summary>
        /// Generate unique file name for image
        /// </summary>
        public static string GenerateUniqueFileName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            var fileName = Path.GetFileNameWithoutExtension(originalFileName);
            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            
            return $"{fileName}_{timestamp}_{uniqueId}{extension}";
        }
    }
}
