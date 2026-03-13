using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;
using Nhom1.Services;

namespace Nhom1.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin/promotions")]
    public class AdminPromotionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPromotionService _promotionService;

        public AdminPromotionsController(ApplicationDbContext context, IPromotionService promotionService)
        {
            _context = context;
            _promotionService = promotionService;
        }

        // GET: admin/promotions
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var promos = await _promotionService.GetActivePromotionsAsync(200);
            return View(promos);
        }

        // GET: admin/promotions/create
        [HttpGet("create")]
        public IActionResult Create()
        {
            return View(new CreatePromotionDto { StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(30) });
        }

        // POST: admin/promotions/create
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePromotionDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            // ensure code unique
            if (await _context.Promotions.AnyAsync(p => p.Code == dto.Code))
            {
                ModelState.AddModelError(nameof(dto.Code), "Mã khuyến mãi đã tồn tại");
                return View(dto);
            }

            var promo = new Promotion
            {
                Code = dto.Code.Trim().ToUpperInvariant(),
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                Value = dto.Value,
                MinOrderAmount = dto.MinOrderAmount,
                MaxDiscountAmount = dto.MaxDiscountAmount,
                IsActive = dto.IsActive,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                UsageLimit = dto.UsageLimit,
                CreatedAt = DateTime.UtcNow
            };

            _context.Promotions.Add(promo);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: admin/promotions/edit/{id}
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null) return NotFound();

            var dto = new UpdatePromotionDto
            {
                Id = promo.Id,
                Code = promo.Code,
                Name = promo.Name,
                Description = promo.Description,
                Type = promo.Type,
                Value = promo.Value,
                MinOrderAmount = promo.MinOrderAmount,
                MaxDiscountAmount = promo.MaxDiscountAmount,
                IsActive = promo.IsActive,
                StartDate = promo.StartDate,
                EndDate = promo.EndDate,
                UsageLimit = promo.UsageLimit
            };

            return View(dto);
        }

        // POST: admin/promotions/edit/{id}
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdatePromotionDto dto)
        {
            if (id != dto.Id) return BadRequest();
            if (!ModelState.IsValid) return View(dto);

            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null) return NotFound();

            // Check code uniqueness
            if (await _context.Promotions.AnyAsync(p => p.Code == dto.Code && p.Id != id))
            {
                ModelState.AddModelError(nameof(dto.Code), "Mã khuyến mãi đã tồn tại");
                return View(dto);
            }

            promo.Code = dto.Code.Trim().ToUpperInvariant();
            promo.Name = dto.Name;
            promo.Description = dto.Description;
            promo.Type = dto.Type;
            promo.Value = dto.Value;
            promo.MinOrderAmount = dto.MinOrderAmount;
            promo.MaxDiscountAmount = dto.MaxDiscountAmount;
            promo.IsActive = dto.IsActive;
            promo.StartDate = dto.StartDate;
            promo.EndDate = dto.EndDate;
            promo.UsageLimit = dto.UsageLimit;
            promo.UpdatedAt = DateTime.UtcNow;

            _context.Promotions.Update(promo);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: admin/promotions/delete/{id}
        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null) return NotFound();

            // Basic safety: do not delete if used by bookings
            if (promo.UsedCount > 0 || promo.Bookings.Any())
            {
                // soft-disable instead
                promo.IsActive = false;
                promo.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            _context.Promotions.Remove(promo);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
