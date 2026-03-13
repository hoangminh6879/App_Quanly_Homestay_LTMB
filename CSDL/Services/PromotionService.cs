using Microsoft.EntityFrameworkCore;
using Nhom1.Data;
using Nhom1.DTOs;
using Nhom1.Models;

namespace Nhom1.Services
{
    public interface IPromotionService
    {
        Task<PromotionDto?> GetByCodeIfApplicableAsync(string code, decimal subTotal);
        Task<Promotion?> GetEntityByCodeIfApplicableAsync(string code, decimal subTotal);
        Task<AmountCalculationDto> CalculateAmountWithPromotionAsync(Promotion? promotion, decimal subtotal);
        Task<bool> IncrementUsageAsync(int promotionId);
        Task<List<PromotionDto>> GetActivePromotionsAsync(int limit = 50);
        // Admin API
        Task<List<PromotionDto>> GetAllPromotionsAsync(int limit = 1000);
        Task<PromotionDto?> GetPromotionByIdAsync(int id);
        Task<PromotionDto> CreatePromotionAsync(CreatePromotionDto dto);
        Task<PromotionDto?> UpdatePromotionAsync(int id, UpdatePromotionDto dto);
        Task<bool> DeletePromotionAsync(int id);
    }

    public class PromotionService : IPromotionService
    {
        private readonly ApplicationDbContext _context;

        public PromotionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PromotionDto?> GetByCodeIfApplicableAsync(string code, decimal subTotal)
        {
            if (string.IsNullOrEmpty(code)) return null;

            var now = DateTime.UtcNow;
            var promo = await GetEntityByCodeIfApplicableAsync(code, subTotal);
            if (promo == null) return null;

            return new PromotionDto
            {
                Id = promo.Id,
                Code = promo.Code,
                Name = promo.Name,
                Description = promo.Description,
                Type = promo.Type,
                Value = promo.Value,
                MinOrderAmount = promo.MinOrderAmount,
                MaxDiscountAmount = promo.MaxDiscountAmount,
                UsageLimit = promo.UsageLimit,
                UsedCount = promo.UsedCount
            };
        }

        public async Task<Promotion?> GetEntityByCodeIfApplicableAsync(string code, decimal subTotal)
        {
            if (string.IsNullOrEmpty(code)) return null;
            var now = DateTime.UtcNow;

            var promo = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Code == code && p.IsActive && p.StartDate <= now && p.EndDate >= now
                                          && (p.UsageLimit == null || p.UsedCount < p.UsageLimit));

            if (promo == null) return null;
            if (promo.MinOrderAmount.HasValue && subTotal < promo.MinOrderAmount.Value) return null;
            return promo;
        }

        public Task<AmountCalculationDto> CalculateAmountWithPromotionAsync(Promotion? promotion, decimal subtotal)
        {
            var dto = new AmountCalculationDto
            {
                Subtotal = subtotal,
                Discount = 0,
                Total = subtotal,
                Nights = 0,
                PricePerNight = 0,
                PromotionApplied = promotion != null,
                PromotionCode = promotion?.Code
            };

            if (promotion != null)
            {
                decimal discount = promotion.Type == PromotionType.Percentage
                    ? subtotal * (promotion.Value / 100)
                    : promotion.Value;

                if (promotion.MaxDiscountAmount.HasValue)
                    discount = Math.Min(discount, promotion.MaxDiscountAmount.Value);

                discount = Math.Min(discount, subtotal);

                dto.Discount = Math.Round(discount, 2);
                dto.Total = Math.Round(subtotal - dto.Discount, 2);
            }

            return Task.FromResult(dto);
        }

        public async Task<bool> IncrementUsageAsync(int promotionId)
        {
            // Increment safely in DB to avoid race conditions
            var promo = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == promotionId);
            if (promo == null) return false;

            promo.UsedCount++;
            promo.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<PromotionDto>> GetActivePromotionsAsync(int limit = 50)
        {
            var now = DateTime.UtcNow;
            var promos = await _context.Promotions
                .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return promos.Select(p => new PromotionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type,
                Value = p.Value,
                MinOrderAmount = p.MinOrderAmount,
                MaxDiscountAmount = p.MaxDiscountAmount,
                UsageLimit = p.UsageLimit,
                UsedCount = p.UsedCount
            }).ToList();
        }

        // Admin implementations
        public async Task<List<PromotionDto>> GetAllPromotionsAsync(int limit = 1000)
        {
            var promos = await _context.Promotions
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return promos.Select(p => new PromotionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type,
                Value = p.Value,
                MinOrderAmount = p.MinOrderAmount,
                MaxDiscountAmount = p.MaxDiscountAmount,
                UsageLimit = p.UsageLimit,
                UsedCount = p.UsedCount,
                IsActive = p.IsActive,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            }).ToList();
        }

        public async Task<PromotionDto?> GetPromotionByIdAsync(int id)
        {
            var p = await _context.Promotions.FindAsync(id);
            if (p == null) return null;
            return new PromotionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type,
                Value = p.Value,
                MinOrderAmount = p.MinOrderAmount,
                MaxDiscountAmount = p.MaxDiscountAmount,
                UsageLimit = p.UsageLimit,
                UsedCount = p.UsedCount,
                IsActive = p.IsActive,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            };
        }

        public async Task<PromotionDto> CreatePromotionAsync(CreatePromotionDto dto)
        {
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

            return await GetPromotionByIdAsync(promo.Id) ?? throw new Exception("Failed to create promotion");
        }

        public async Task<PromotionDto?> UpdatePromotionAsync(int id, UpdatePromotionDto dto)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null) return null;

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

            return await GetPromotionByIdAsync(promo.Id);
        }

        public async Task<bool> DeletePromotionAsync(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null) return false;

            // If used, soft-disable; else delete
            if (promo.UsedCount > 0 || promo.Bookings.Any())
            {
                promo.IsActive = false;
                promo.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }

            _context.Promotions.Remove(promo);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
