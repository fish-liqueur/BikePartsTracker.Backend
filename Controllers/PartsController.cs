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
    public class PartsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPartUsageTrackingService _usageTracking;
        private readonly IWorkEvaluationService _workEvaluationService;

        public PartsController(
            AppDbContext context,
            IPartUsageTrackingService usageTracking,
            IWorkEvaluationService workEvaluationService)
        {
            _context = context;
            _usageTracking = usageTracking;
            _workEvaluationService = workEvaluationService;
        }

        // GET: api/Parts
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BikePartDto>>> GetParts()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var parts = await _context.BikeParts
                .Where(p => p.UserId == userId)
                .ToListAsync();

            return Ok(await MapPartsAsync(userId, parts));
        }

        // GET: api/Parts/bike/5
        [HttpGet("bike/{bikeId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BikePartDto>>> GetPartsByBike(Guid bikeId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var bike = await _context.Bikes
                .FirstOrDefaultAsync(b => b.Id == bikeId && b.UserId == userId);

            if (bike == null)
            {
                return NotFound();
            }

            var parts = await _context.BikeParts
                .Where(p => p.BikeId == bikeId)
                .ToListAsync();

            return Ok(await MapPartsAsync(userId, parts));
        }

        // GET: api/Parts/5
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<BikePartDto>> GetPart(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var part = await _context.BikeParts.FirstOrDefaultAsync(p => p.Id == id);

            if (part == null)
            {
                return NotFound();
            }

            if (part.UserId != userId)
            {
                return Forbid();
            }

            var dto = await MapPartAsync(userId, part);
            return Ok(dto);
        }

        // Batch endpoints used by the frontend to flush its dirty-set after a ride mutation.
        // Each response key is a part id; ids not owned by the user are silently dropped so the
        // client can detect deletions/foreign rows by their absence.
        private const int BatchPartsMaxIds = 200;

        // POST: api/Parts/batch
        // Returns part summaries (TotalDistance, PendingWorksCount) only. Usage history lives
        // behind the dedicated history endpoints below.
        [HttpPost("batch")]
        [Authorize]
        public async Task<ActionResult<Dictionary<Guid, BikePartDto>>> BatchParts([FromBody] BatchPartIdsRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            if (request.PartIds.Count > BatchPartsMaxIds)
            {
                return BadRequest(new { message = $"At most {BatchPartsMaxIds} part ids can be requested per call." });
            }

            var distinctIds = request.PartIds.Distinct().ToList();

            var ownedParts = await _context.BikeParts
                .Where(p => distinctIds.Contains(p.Id) && p.UserId == userId)
                .ToListAsync();

            var partDtos = await MapPartsAsync(userId, ownedParts);

            return Ok(partDtos.ToDictionary(p => p.Id, p => p));
        }

        // GET: api/Parts/{id}/history
        // Non-shadow usage history for a single part, sorted by StartDate ascending.
        [HttpGet("{id}/history")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<UsagePeriodDto>>> GetPartHistory(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var partExists = await _context.BikeParts
                .AnyAsync(p => p.Id == id && p.UserId == userId);

            if (!partExists)
            {
                return NotFound();
            }

            var history = await _context.PartUsageHistories
                .Where(h => h.BikePartId == id && !h.IsShadow)
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

            return Ok(history);
        }

        // POST: api/Parts/batch/history
        // Multi-fetch histories. Known owned parts with no records appear as empty arrays.
        [HttpPost("batch/history")]
        [Authorize]
        public async Task<ActionResult<Dictionary<Guid, List<UsagePeriodDto>>>> BatchPartsHistory([FromBody] BatchPartIdsRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            if (request.PartIds.Count > BatchPartsMaxIds)
            {
                return BadRequest(new { message = $"At most {BatchPartsMaxIds} part ids can be requested per call." });
            }

            var distinctIds = request.PartIds.Distinct().ToList();

            var ownedIds = await _context.BikeParts
                .Where(p => distinctIds.Contains(p.Id) && p.UserId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            var response = ownedIds.ToDictionary(id => id, _ => new List<UsagePeriodDto>());

            if (ownedIds.Count == 0)
            {
                return Ok(response);
            }

            var rows = await _context.PartUsageHistories
                .Where(h => ownedIds.Contains(h.BikePartId) && !h.IsShadow)
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

            foreach (var row in rows)
            {
                response[row.BikePartId].Add(row);
            }

            return Ok(response);
        }

        // POST: api/Parts
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<BikePartDto>> PostPart([FromBody] CreatePartDto createPartDto)
        {
            Console.WriteLine("PostPart called");
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get current user from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            // Verify the bike belongs to the current user if BikeId is provided
            if (createPartDto.BikeId.HasValue)
            {
                var bike = await _context.Bikes
                    .FirstOrDefaultAsync(b => b.Id == createPartDto.BikeId.Value && b.UserId == userId);

                if (bike == null)
                {
                    return BadRequest("Bike not found or does not belong to the current user");
                }
            }

            var now = DateTime.UtcNow;
            var part = new BikePart
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BikeId = createPartDto.BikeId,
                Name = createPartDto.Name,
                Description = createPartDto.Description,
                PartType = createPartDto.PartType,
                Type = createPartDto.PartType.ToString(),
                Brand = createPartDto.Brand,
                Model = createPartDto.Model,
                InstallationDate = createPartDto.InstallationDate ?? now,
                MileageAtInstallation = createPartDto.MileageAtInstallation,
                HistoryJson = "[]",
                ScheduleType = PartScheduleType.OneTimeUse,
                ScheduleValue = 0.0,
                IsActive = createPartDto.IsActive ?? true,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.BikeParts.Add(part);
            await _context.SaveChangesAsync();

            if (part.BikeId.HasValue)
            {
                await _usageTracking.OpenUsagePeriodAsync(part, part.BikeId.Value, part.InstallationDate ?? now);
            }

            var createdPart = await _context.BikeParts.FirstOrDefaultAsync(p => p.Id == part.Id);
            var dto = await MapPartAsync(userId, createdPart);

            return CreatedAtAction(nameof(GetPart), new { id = part.Id }, dto);
        }

        // PUT: api/Parts/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<object>> PutPart(Guid id, [FromBody] UpdatePartDto updatePartDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get current user from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var part = await _context.BikeParts.FirstOrDefaultAsync(p => p.Id == id);

            if (part == null)
            {
                return NotFound();
            }

            if (part.UserId != userId)
            {
                return Forbid();
            }

            var affectedChainCycles = new List<ChainCycleResponseDto>();
            var oldBikeId = part.BikeId;

            // Handle BikeId: Guid.Empty means "clear", null means "don't change"
            if (updatePartDto.BikeId.HasValue)
            {
                if (updatePartDto.BikeId.Value == Guid.Empty)
                {
                    part.BikeId = null;
                }
                else if (updatePartDto.BikeId.Value != part.BikeId)
                {
                    var newBike = await _context.Bikes
                        .FirstOrDefaultAsync(b => b.Id == updatePartDto.BikeId.Value && b.UserId == userId);

                    if (newBike == null)
                        return BadRequest("Bike not found or does not belong to the current user");

                    part.BikeId = updatePartDto.BikeId.Value;
                }
            }

            // Cascade: if bike changed or cleared, remove part from any chain cycles on the old bike
            if (oldBikeId.HasValue && part.BikeId != oldBikeId)
            {
                affectedChainCycles = await RemovePartFromCycles(part.Id, oldBikeId.Value);
            }

            if (updatePartDto.Name != null)
                part.Name = updatePartDto.Name;

            if (updatePartDto.Description != null)
                part.Description = updatePartDto.Description;

            if (updatePartDto.PartType.HasValue)
            {
                part.PartType = updatePartDto.PartType.Value;
                part.Type = updatePartDto.PartType.Value.ToString();
            }

            if (updatePartDto.Brand != null)
                part.Brand = updatePartDto.Brand;

            if (updatePartDto.Model != null)
                part.Model = updatePartDto.Model;

            if (updatePartDto.InstallationDate.HasValue)
                part.InstallationDate = updatePartDto.InstallationDate.Value;

            if (updatePartDto.MileageAtInstallation.HasValue)
                part.MileageAtInstallation = updatePartDto.MileageAtInstallation.Value;

            if (updatePartDto.ScheduleType.HasValue)
                part.ScheduleType = updatePartDto.ScheduleType.Value;

            if (updatePartDto.ScheduleValue.HasValue)
                part.ScheduleValue = updatePartDto.ScheduleValue.Value;

            if (updatePartDto.IsActive.HasValue)
                part.IsActive = updatePartDto.IsActive.Value;

            part.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PartExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            if (oldBikeId != part.BikeId)
            {
                if (oldBikeId.HasValue)
                {
                    await _usageTracking.CloseOpenUsagePeriodsAsync(part.Id, DateTime.UtcNow);
                }

                if (part.BikeId.HasValue)
                {
                    await _usageTracking.OpenUsagePeriodAsync(
                        part,
                        part.BikeId.Value,
                        part.InstallationDate ?? DateTime.UtcNow);
                }
            }

            var updatedPart = await _context.BikeParts.FirstOrDefaultAsync(p => p.Id == id);
            var dto = await MapPartAsync(userId, updatedPart);

            return Ok(new { part = dto, affectedChainCycles });
        }

        // DELETE: api/Parts/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult<object>> DeletePart(Guid id)
        {
            // Get current user from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var part = await _context.BikeParts.FirstOrDefaultAsync(p => p.Id == id);

            if (part == null)
            {
                return NotFound();
            }

            if (part.UserId != userId)
            {
                return Forbid();
            }

            var affectedChainCycles = new List<ChainCycleResponseDto>();

            if (part.BikeId.HasValue)
            {
                affectedChainCycles = await RemovePartFromCycles(part.Id, part.BikeId.Value);
            }

            _context.BikeParts.Remove(part);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, affectedChainCycles });
        }

        // GET: api/Parts/search?q=query
        [HttpGet("search")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BikePartDto>>> SearchParts([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query is required");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var searchTerm = q.ToLower();
            var parts = await _context.BikeParts
                .Where(p => p.UserId == userId &&
                    (p.Name.ToLower().Contains(searchTerm) ||
                     (p.Brand != null && p.Brand.ToLower().Contains(searchTerm)) ||
                     (p.Model != null && p.Model.ToLower().Contains(searchTerm)) ||
                     (p.Description != null && p.Description.ToLower().Contains(searchTerm))))
                .ToListAsync();

            return Ok(await MapPartsAsync(userId, parts));
        }

        // GET: api/Parts/type?type=Chain
        [HttpGet("type")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BikePartDto>>> GetPartsByType([FromQuery] string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return BadRequest("Part type is required");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            if (!Enum.TryParse<PartType>(type, ignoreCase: true, out var partType))
            {
                return BadRequest($"Invalid part type: {type}");
            }

            var parts = await _context.BikeParts
                .Where(p => p.UserId == userId && p.PartType == partType)
                .ToListAsync();

            return Ok(await MapPartsAsync(userId, parts));
        }

        /// <summary>
        /// Removes a part ID from the Chains array of all cycles on a given bike.
        /// Replaces the part ID with null (preserving slot) and clears ActiveChainId if needed.
        /// Returns the list of cycles that were modified.
        /// </summary>
        private async Task<List<ChainCycleResponseDto>> RemovePartFromCycles(Guid partId, Guid bikeId)
        {
            var affected = new List<ChainCycleResponseDto>();

            var cycles = await _context.ChainCycles
                .Where(c => c.BikeId == bikeId)
                .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var cycle in cycles)
            {
                var chains = cycle.Chains;
                bool modified = false;

                for (int i = 0; i < chains.Count; i++)
                {
                    if (chains[i] == partId)
                    {
                        chains[i] = null;
                        modified = true;
                    }
                }

                if (cycle.ActiveChainId == partId)
                {
                    cycle.ActiveChainId = null;
                    modified = true;
                }

                if (modified)
                {
                    cycle.Chains = chains;
                    cycle.UpdatedAt = now;
                    affected.Add(ChainCyclesController.MapToDto(cycle));
                }
            }

            return affected;
        }

        private bool PartExists(Guid id)
        {
            return _context.BikeParts.Any(e => e.Id == id);
        }

        /// <summary>
        /// Projects a list of <see cref="BikePart"/> entities to <see cref="BikePartDto"/> with
        /// computed <c>TotalDistance</c> and <c>PendingWorksCount</c> summaries. Detailed usage
        /// history is fetched separately via the dedicated usage-periods endpoint.
        /// </summary>
        private async Task<List<BikePartDto>> MapPartsAsync(Guid userId, List<BikePart> parts)
        {
            if (parts.Count == 0)
            {
                return new List<BikePartDto>();
            }

            var partIds = parts.Select(p => p.Id).ToList();

            var distanceRows = await _context.PartUsageHistories
                .Where(h => partIds.Contains(h.BikePartId) && !h.IsShadow)
                .GroupBy(h => h.BikePartId)
                .Select(g => new { PartId = g.Key, Total = g.Sum(h => h.Distance) })
                .ToListAsync();

            var distances = distanceRows.ToDictionary(x => x.PartId, x => x.Total);

            var works = await _context.Works
                .Where(w => w.UserId == userId &&
                            w.IsActive &&
                            w.ParentType == WorkParentType.Part &&
                            partIds.Contains(w.ParentId))
                .ToListAsync();

            var pending = new Dictionary<Guid, int>();
            foreach (var work in works)
            {
                var consumed = await _workEvaluationService.GetConsumedValueAsync(work);
                if (consumed >= work.TriggerValue)
                {
                    pending[work.ParentId] = pending.GetValueOrDefault(work.ParentId) + 1;
                }
            }

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
                PendingWorksCount = pending.TryGetValue(p.Id, out var c) ? c : 0,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();
        }

        private async Task<BikePartDto?> MapPartAsync(Guid userId, BikePart? part)
        {
            if (part == null)
            {
                return null;
            }

            var list = await MapPartsAsync(userId, new List<BikePart> { part });
            return list.FirstOrDefault();
        }
    }
}
