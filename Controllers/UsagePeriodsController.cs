using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Extensions;
using BikePartsTracker.Models;
using BikePartsTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsagePeriodsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IUsagePeriodDistanceService _usagePeriodDistanceService;
        private readonly IWorkShadowPeriodService _workShadowPeriodService;

        public UsagePeriodsController(
            AppDbContext context,
            IUsagePeriodDistanceService usagePeriodDistanceService,
            IWorkShadowPeriodService workShadowPeriodService)
        {
            _context = context;
            _usagePeriodDistanceService = usagePeriodDistanceService;
            _workShadowPeriodService = workShadowPeriodService;
        }

        [HttpGet("part/{bikePartId}")]
        public async Task<ActionResult<IEnumerable<UsagePeriodDto>>> GetByPart(Guid bikePartId, [FromQuery] bool includeShadow = false)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var query = _context.PartUsageHistories
                .Include(h => h.BikePart)
                .Where(h => h.BikePartId == bikePartId && h.BikePart.UserId == userId);

            if (!includeShadow)
            {
                query = query.Where(h => !h.IsShadow);
            }

            var periods = await query
                .OrderBy(h => h.StartDate)
                .Select(h => new UsagePeriodDto
                {
                    Id = h.Id,
                    BikePartId = h.BikePartId,
                    BikeId = h.BikeId,
                    StartDate = h.StartDate,
                    EndDate = h.EndDate,
                    Distance = h.Distance,
                    IsShadow = h.IsShadow,
                    WorkId = h.WorkId,
                    SourceUsagePeriodId = h.SourceUsagePeriodId,
                    Notes = h.Notes
                })
                .ToListAsync();

            return Ok(periods);
        }

        [HttpPost]
        public async Task<ActionResult<UsagePeriodDto>> Create([FromBody] CreateUsagePeriodDto dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            if (dto.EndDate.HasValue && dto.EndDate.Value < dto.StartDate)
            {
                return BadRequest(new { message = "EndDate must be greater than or equal to StartDate." });
            }

            var part = await _context.BikeParts.FirstOrDefaultAsync(p => p.Id == dto.BikePartId && p.UserId == userId);
            if (part == null)
            {
                return BadRequest(new { message = "Part not found for user." });
            }

            if (dto.BikeId.HasValue)
            {
                var bikeExists = await _context.Bikes.AnyAsync(b => b.Id == dto.BikeId.Value && b.UserId == userId);
                if (!bikeExists)
                {
                    return BadRequest(new { message = "Bike not found for user." });
                }
            }

            var period = new PartUsageHistory
            {
                Id = Guid.NewGuid(),
                BikePartId = dto.BikePartId,
                BikePart = part,
                BikeId = dto.BikeId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsShadow = false,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _usagePeriodDistanceService.RecalculatePeriodDistanceAsync(period);
            _context.PartUsageHistories.Add(period);
            await _context.SaveChangesAsync();

            await _workShadowPeriodService.SyncShadowPeriodsForPartAsync(dto.BikePartId);

            return CreatedAtAction(nameof(GetByPart), new { bikePartId = dto.BikePartId }, MapToDto(period));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UsagePeriodDto>> Update(Guid id, [FromBody] UpdateUsagePeriodDto dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var period = await _context.PartUsageHistories
                .Include(h => h.BikePart)
                .FirstOrDefaultAsync(h => h.Id == id && h.BikePart.UserId == userId);

            if (period == null)
            {
                return NotFound();
            }

            if (period.IsShadow)
            {
                return BadRequest(new { message = "Shadow usage periods cannot be edited directly." });
            }

            if (dto.BikeId.HasValue)
            {
                var bikeExists = await _context.Bikes.AnyAsync(b => b.Id == dto.BikeId.Value && b.UserId == userId);
                if (!bikeExists)
                {
                    return BadRequest(new { message = "Bike not found for user." });
                }
                period.BikeId = dto.BikeId;
            }

            if (dto.StartDate.HasValue)
            {
                period.StartDate = dto.StartDate.Value;
            }

            if (dto.EndDate.HasValue)
            {
                period.EndDate = dto.EndDate.Value;
            }

            if (period.EndDate.HasValue && period.EndDate.Value < period.StartDate)
            {
                return BadRequest(new { message = "EndDate must be greater than or equal to StartDate." });
            }

            if (dto.Notes != null)
            {
                period.Notes = dto.Notes;
            }

            await _usagePeriodDistanceService.RecalculatePeriodDistanceAsync(period);
            await _context.SaveChangesAsync();

            await _workShadowPeriodService.SyncShadowPeriodsForPartAsync(period.BikePartId);

            return Ok(MapToDto(period));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var period = await _context.PartUsageHistories
                .Include(h => h.BikePart)
                .FirstOrDefaultAsync(h => h.Id == id && h.BikePart.UserId == userId);

            if (period == null)
            {
                return NotFound();
            }

            if (period.IsShadow)
            {
                return BadRequest(new { message = "Shadow usage periods cannot be deleted directly." });
            }

            var bikePartId = period.BikePartId;
            _context.PartUsageHistories.Remove(period);
            await _context.SaveChangesAsync();

            await _workShadowPeriodService.SyncShadowPeriodsForPartAsync(bikePartId);
            return NoContent();
        }

        private static UsagePeriodDto MapToDto(PartUsageHistory h) => new()
        {
            Id = h.Id,
            BikePartId = h.BikePartId,
            BikeId = h.BikeId,
            StartDate = h.StartDate,
            EndDate = h.EndDate,
            Distance = h.Distance,
            IsShadow = h.IsShadow,
            WorkId = h.WorkId,
            SourceUsagePeriodId = h.SourceUsagePeriodId,
            Notes = h.Notes
        };

    }
}
