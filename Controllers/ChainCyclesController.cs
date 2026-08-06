using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BikePartsTracker.Data;
using BikePartsTracker.Models;
using BikePartsTracker.DTOs;
using BikePartsTracker.Services;

namespace BikePartsTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChainCyclesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPartUsageTrackingService _usageTracking;

        public ChainCyclesController(AppDbContext context, IPartUsageTrackingService usageTracking)
        {
            _context = context;
            _usageTracking = usageTracking;
        }

        // GET: api/ChainCycles?bikeId={bikeId}
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ChainCycleResponseDto>>> GetChainCycles([FromQuery] Guid bikeId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var bike = await _context.Bikes
                .FirstOrDefaultAsync(b => b.Id == bikeId && b.UserId == userId);

            if (bike == null) return NotFound("Bike not found");

            var cycles = await _context.ChainCycles
                .Where(c => c.BikeId == bikeId)
                .ToListAsync();

            return Ok(cycles.Select(MapToDto));
        }

        // GET: api/ChainCycles/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ChainCycleResponseDto>> GetChainCycle(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var cycle = await _context.ChainCycles
                .Include(c => c.Bike)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cycle == null) return NotFound();
            if (cycle.Bike.UserId != userId) return Forbid();

            return Ok(MapToDto(cycle));
        }

        // POST: api/ChainCycles
        [HttpPost]
        public async Task<ActionResult<ChainCycleResponseDto>> PostChainCycle([FromBody] CreateChainCycleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var bike = await _context.Bikes
                .FirstOrDefaultAsync(b => b.Id == dto.BikeId && b.UserId == userId);

            if (bike == null)
                return BadRequest("Bike not found or does not belong to the current user");

            var now = DateTime.UtcNow;
            var cycle = new ChainCycle
            {
                Id = Guid.NewGuid(),
                BikeId = dto.BikeId,
                Chains = dto.Chains ?? new List<Guid?>(),
                ActiveChainId = dto.ActiveChainId,
                IntervalMetres = dto.IntervalMetres,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.ChainCycles.Add(cycle);
            await _context.SaveChangesAsync();

            if (cycle.ActiveChainId.HasValue)
            {
                var activeChain = await _context.BikeParts
                    .FirstOrDefaultAsync(p => p.Id == cycle.ActiveChainId.Value && p.UserId == userId);
                if (activeChain != null)
                {
                    await _usageTracking.OpenUsagePeriodAsync(activeChain, cycle.BikeId, now);
                }
            }

            return CreatedAtAction(nameof(GetChainCycle), new { id = cycle.Id }, MapToDto(cycle));
        }

        // PUT: api/ChainCycles/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<ChainCycleResponseDto>> PutChainCycle(Guid id, [FromBody] UpdateChainCycleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var cycle = await _context.ChainCycles
                .Include(c => c.Bike)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cycle == null) return NotFound();
            if (cycle.Bike.UserId != userId) return Forbid();

            var oldActiveChainId = cycle.ActiveChainId;

            if (dto.Chains != null)
                cycle.Chains = dto.Chains;

            if (dto.ActiveChainId.HasValue)
                cycle.ActiveChainId = dto.ActiveChainId.Value == Guid.Empty ? null : dto.ActiveChainId.Value;

            if (dto.IntervalMetres.HasValue)
                cycle.IntervalMetres = dto.IntervalMetres.Value;

            var now = DateTime.UtcNow;
            cycle.UpdatedAt = now;

            await _context.SaveChangesAsync();

            if (oldActiveChainId != cycle.ActiveChainId)
            {
                if (oldActiveChainId.HasValue)
                {
                    await _usageTracking.CloseOpenUsagePeriodsAsync(oldActiveChainId.Value, now);
                }

                if (cycle.ActiveChainId.HasValue)
                {
                    var newActiveChain = await _context.BikeParts
                        .FirstOrDefaultAsync(p => p.Id == cycle.ActiveChainId.Value && p.UserId == userId);
                    if (newActiveChain != null)
                    {
                        await _usageTracking.OpenUsagePeriodAsync(newActiveChain, cycle.BikeId, now);
                    }
                }
            }

            return Ok(MapToDto(cycle));
        }

        // DELETE: api/ChainCycles/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteChainCycle(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var cycle = await _context.ChainCycles
                .Include(c => c.Bike)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cycle == null) return NotFound();
            if (cycle.Bike.UserId != userId) return Forbid();

            if (cycle.ActiveChainId.HasValue)
            {
                await _usageTracking.CloseOpenUsagePeriodsAsync(cycle.ActiveChainId.Value, DateTime.UtcNow);
            }

            _context.ChainCycles.Remove(cycle);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
                return null;
            return userId;
        }

        internal static ChainCycleResponseDto MapToDto(ChainCycle cycle) => new()
        {
            Id = cycle.Id,
            BikeId = cycle.BikeId,
            Chains = cycle.Chains,
            ActiveChainId = cycle.ActiveChainId,
            IntervalMetres = cycle.IntervalMetres,
            CreatedAt = cycle.CreatedAt,
            UpdatedAt = cycle.UpdatedAt
        };
    }
}
