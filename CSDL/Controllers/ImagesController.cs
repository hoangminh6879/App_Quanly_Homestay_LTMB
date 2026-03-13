using Microsoft.AspNetCore.Mvc;
using Nhom1.Configurations;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public ImagesController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        /// <summary>
        /// Get all available image paths and folders
        /// </summary>
        [HttpGet("list")]
        public IActionResult GetImagesList()
        {
            var response = new
            {
                message = "Available images in wwwroot/images",
                defaultImages = new
                {
                    defaultHomestay = ImagePaths.DefaultHomestay,
                    placeholderHomestay = ImagePaths.PlaceholderHomestay,
                    defaultAvatar = ImagePaths.DefaultAvatar,
                    defaultAvatarSvg = ImagePaths.DefaultAvatarSvg,
                    noImage = ImagePaths.NoImage,
                    adminLogo = ImagePaths.AdminLogo
                },
                folders = new
                {
                    homestays = ImagePaths.HomestaysFolder,
                    users = ImagePaths.UsersFolder,
                    admin = ImagePaths.AdminFolder,
                    tudong = ImagePaths.TudongFolder,
                    placeholders = ImagePaths.PlaceholdersFolder
                },
                homestaySubfolders = new
                {
                    bathrooms = ImagePaths.HomestaysBathrooms,
                    bedrooms = ImagePaths.HomestaysBedrooms,
                    commonAreas = ImagePaths.HomestaysCommonAreas,
                    exterior = ImagePaths.HomestaysExterior
                },
                tudongSampleImages = ImagePaths.GetAllTudongImages(),
                usage = new
                {
                    getHomestayImage = "Use ImageHelper.GetHomestayImageUrl(url)",
                    getUserAvatar = "Use ImageHelper.GetUserAvatarUrl(url)",
                    buildFullUrl = "Use ImageHelper.BuildFullImageUrl(baseUrl, url)"
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// Test image URL processing with different formats
        /// </summary>
        [HttpGet("test-urls")]
        public IActionResult TestImageUrls()
        {
            var testCases = new[]
            {
                new { input = (string?)null, output = ImageHelper.GetHomestayImageUrl(null), description = "Null URL -> placeholder" },
                new { input = (string?)"", output = ImageHelper.GetHomestayImageUrl(""), description = "Empty URL -> placeholder" },
                new { input = (string?)"/images/test.jpg", output = ImageHelper.GetHomestayImageUrl("/images/test.jpg"), description = "Absolute path with /" },
                new { input = (string?)"homestays/test.jpg", output = ImageHelper.GetHomestayImageUrl("homestays/test.jpg"), description = "Relative path -> prepend /images/" },
                new { input = (string?)"https://example.com/image.jpg", output = ImageHelper.GetHomestayImageUrl("https://example.com/image.jpg"), description = "Full HTTPS URL -> keep as is" },
                new { input = (string?)"http://example.com/image.jpg", output = ImageHelper.GetHomestayImageUrl("http://example.com/image.jpg"), description = "Full HTTP URL -> keep as is" },
            };

            return Ok(new
            {
                message = "Image URL processing test cases",
                testCases = testCases
            });
        }

        /// <summary>
        /// Scan and list all actual files in images directory
        /// </summary>
        [HttpGet("scan")]
        public IActionResult ScanImagesDirectory()
        {
            var imagesPath = Path.Combine(_environment.WebRootPath, "images");
            
            if (!Directory.Exists(imagesPath))
            {
                return NotFound(new { message = "Images directory not found" });
            }

            var files = new List<object>();
            ScanDirectory(imagesPath, imagesPath, files);

            return Ok(new
            {
                message = "Scanned images directory",
                totalFiles = files.Count,
                files = files.OrderBy(f => ((dynamic)f).path)
            });
        }

        private void ScanDirectory(string rootPath, string currentPath, List<object> files)
        {
            try
            {
                // Get all files in current directory
                foreach (var file in Directory.GetFiles(currentPath))
                {
                    var relativePath = Path.GetRelativePath(rootPath, file).Replace("\\", "/");
                    var webPath = $"/images/{relativePath}";
                    var fileInfo = new FileInfo(file);
                    
                    files.Add(new
                    {
                        name = Path.GetFileName(file),
                        path = webPath,
                        size = FormatFileSize(fileInfo.Length),
                        sizeBytes = fileInfo.Length,
                        extension = fileInfo.Extension,
                        lastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                // Recursively scan subdirectories
                foreach (var directory in Directory.GetDirectories(currentPath))
                {
                    ScanDirectory(rootPath, directory, files);
                }
            }
            catch (Exception ex)
            {
                files.Add(new
                {
                    error = ex.Message,
                    path = currentPath
                });
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
