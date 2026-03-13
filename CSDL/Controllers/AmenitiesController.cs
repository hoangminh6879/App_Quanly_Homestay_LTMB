using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AmenitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AmenitiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAllAmenities()
        {
            var amenities = await _context.Amenities
                .OrderBy(a => a.Name)
                .Select(a => new AmenitiesListDto
                {
                    Id = a.Id,
                    Name = a.Name ?? string.Empty,
                    Icon = a.Icon ?? string.Empty,
                    Description = a.Description ?? string.Empty
                })
                .ToListAsync();

            return Ok(ApiResponse<List<AmenitiesListDto>>.SuccessResponse(amenities));
        }
    }
}
