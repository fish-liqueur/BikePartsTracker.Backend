using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BikePartsTracker.Data;
using BikePartsTracker.Models;
using BikePartsTracker.DTOs;
using BikePartsTracker.Services;
using BikePartsTracker.Exceptions;
using BikePartsTracker.Localization;

namespace BikePartsTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChainCyclesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPartUsageTrackingService _usageTracking;
        private readonly IFillEmptySlotsFaultInjector _fillFaultInjector;

        public ChainCyclesController(
            AppDbContext context,
            IPartUsageTrackingService usageTracking,
            IFillEmptySlotsFaultInjector fillFaultInjector)
        {
            _context = context;
            _usageTracking = usageTracking;
            _fillFaultInjector = fillFaultInjector;
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

        // POST: api/ChainCycles/{id}/fill-empty-slots (ADR 0010)
        [HttpPost("{id}/fill-empty-slots")]
        public async Task<ActionResult<FillEmptyChainCycleSlotsResponseDto>> FillEmptySlots(
            Guid id,
            [FromBody] FillEmptyChainCycleSlotsDto? dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            dto ??= new FillEmptyChainCycleSlotsDto();

            var cycle = await _context.ChainCycles
                .Include(c => c.Bike)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cycle == null) return NotFound();
            if (cycle.Bike.UserId != userId) return Forbid();

            var chains = cycle.Chains.ToList();
            var emptyIndices = new List<int>();
            for (var i = 0; i < chains.Count; i++)
            {
                if (chains[i] == null)
                    emptyIndices.Add(i);
            }

            if (emptyIndices.Count == 0)
                throw new AppException(ErrorCodes.ChainCyclesNoEmptySlots);

            var hadActive = cycle.ActiveChainId.HasValue;
            BikePart? newlyActivated = null;
            DateTime? installAt = null;

            if (!hadActive && dto.ActiveNewSlotIndex.HasValue)
            {
                var idx = dto.ActiveNewSlotIndex.Value;
                if (idx < 0 || idx >= chains.Count || chains[idx] != null)
                    throw new AppException(ErrorCodes.ChainCyclesInvalidActiveSlot);
                if (!emptyIndices.Contains(idx))
                    throw new AppException(ErrorCodes.ChainCyclesInvalidActiveSlot);

                installAt = dto.InstallationDate ?? DateTime.UtcNow;
            }

            var bikeName = cycle.Bike.Name;
            var bikeId = cycle.BikeId;
            var bikeTotalDistance = cycle.Bike.TotalDistance;
            var now = DateTime.UtcNow;
            var createdParts = new List<BikePart>();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var slotIndex in emptyIndices)
                {
                    var isBecomingActive = !hadActive
                        && dto.ActiveNewSlotIndex.HasValue
                        && dto.ActiveNewSlotIndex.Value == slotIndex;

                    var part = new BikePart
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId.Value,
                        BikeId = bikeId,
                        Name = $"{bikeName} chain {slotIndex + 1}",
                        PartType = PartType.Chain,
                        Type = PartType.Chain.ToString(),
                        HistoryJson = "[]",
                        ScheduleType = PartScheduleType.OneTimeUse,
                        ScheduleValue = 0.0,
                        IsActive = true,
                        InstallationDate = isBecomingActive ? installAt : null,
                        MileageAtInstallation = isBecomingActive ? bikeTotalDistance : null,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    _context.BikeParts.Add(part);
                    chains[slotIndex] = part.Id;
                    createdParts.Add(part);

                    if (isBecomingActive)
                        newlyActivated = part;

                    // Persist per slot so a mid-loop fault (BE-09) exercises transaction rollback.
                    cycle.Chains = chains;
                    cycle.UpdatedAt = now;
                    await _context.SaveChangesAsync();
                    await _fillFaultInjector.OnAfterPartAddedAsync(createdParts.Count);
                }

                if (newlyActivated != null)
                {
                    cycle.ActiveChainId = newlyActivated.Id;
                    await _context.SaveChangesAsync();
                    await _usageTracking.OpenUsagePeriodAsync(newlyActivated, bikeId, installAt!.Value);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            var createdDtos = await MapCreatedPartsAsync(createdParts);
            var affectedIds = createdParts.Select(p => p.Id).ToList();

            return Ok(new FillEmptyChainCycleSlotsResponseDto
            {
                ChainCycle = MapToDto(cycle),
                CreatedParts = createdDtos,
                AffectedPartIds = affectedIds
            });
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

        private async Task<List<BikePartDto>> MapCreatedPartsAsync(List<BikePart> parts)
        {
            if (parts.Count == 0)
                return new List<BikePartDto>();

            var partIds = parts.Select(p => p.Id).ToList();
            var distanceRows = await _context.PartUsageHistories
                .Where(h => partIds.Contains(h.BikePartId) && !h.IsShadow)
                .GroupBy(h => h.BikePartId)
                .Select(g => new { PartId = g.Key, Total = g.Sum(h => h.Distance) })
                .ToListAsync();
            var distances = distanceRows.ToDictionary(x => x.PartId, x => x.Total);

            return parts.Select(p => new BikePartDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                PartType = p.PartType,
                Brand = p.Brand,
                Model = p.Model,
                InstallationDate = p.InstallationDate,
                MileageAtInstallation = p.MileageAtInstallation,
                BikeId = p.BikeId,
                IsActive = p.IsActive,
                TotalDistance = distances.TryGetValue(p.Id, out var d) ? d : 0,
                PendingMaintenanceTasksCount = 0,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();
        }
    }
}
