using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Nhom1.DTOs;
using Nhom1.Services;

namespace Nhom1.Controllers
{
    [ApiController]
    [Route("api/promotions")]
    public class PromotionsApiController : ControllerBase
    {
        private readonly IPromotionService _promotionService;
        private readonly ILogger<PromotionsApiController> _logger;

        public PromotionsApiController(IPromotionService promotionService, ILogger<PromotionsApiController> logger)
        {
            _promotionService = promotionService;
            _logger = logger;
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActivePromotions()
        {
            var list = await _promotionService.GetActivePromotionsAsync();
            return Ok(list);
        }

        [HttpGet("validate")]
        public async Task<IActionResult> Validate([FromQuery] string code, [FromQuery] decimal subtotal)
        {
            if (string.IsNullOrEmpty(code)) return BadRequest(new { message = "Code is required" });

            var promo = await _promotionService.GetByCodeIfApplicableAsync(code, subtotal);
            if (promo == null) return NotFound(new { message = "Promotion not applicable" });

            // Map to small response with calculation
            // We'll return basic promotion info and let client call calculation if needed
            return Ok(promo);
        }

        // Admin CRUD endpoints
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllPromotions()
        {
            try
            {
                // Diagnostic logging to help debug 404/authorization issues
                _logger.LogInformation("GetAllPromotions called. IsAuthenticated={IsAuthenticated}", User?.Identity?.IsAuthenticated);
                if (Request.Headers.ContainsKey("Authorization"))
                {
                    _logger.LogInformation("Authorization header present: {Auth}", Request.Headers["Authorization"].ToString());
                }

                foreach (var claim in User?.Claims ?? Enumerable.Empty<System.Security.Claims.Claim>())
                {
                    _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                }

                var list = await _promotionService.GetAllPromotionsAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllPromotions");
                throw;
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPromotion(int id)
        {
            var promo = await _promotionService.GetPromotionByIdAsync(id);
            if (promo == null) return NotFound();
            return Ok(promo);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            // Ensure unique code
            var created = await _promotionService.CreatePromotionAsync(dto);
            return CreatedAtAction(nameof(GetPromotion), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePromotion(int id, [FromBody] UpdatePromotionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (id != dto.Id) return BadRequest();

            var updated = await _promotionService.UpdatePromotionAsync(id, dto);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            var ok = await _promotionService.DeletePromotionAsync(id);
            if (!ok) return NotFound();
            return NoContent();
        }
    }
}
