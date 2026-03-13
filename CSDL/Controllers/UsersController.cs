using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Nhom1.Data;
using System.Threading.Tasks;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public UsersController(ApplicationDbContext db) => _db = db;

        // GET api/users/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new
                {
                    id = u.Id,
                    displayName = (u.FirstName + " " + u.LastName).Trim(),
                    phoneNumber = u.PhoneNumber,
                    avatar = u.ProfilePicture
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();
            return Ok(user);
        }
    }
}
