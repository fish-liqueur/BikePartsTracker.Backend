using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BikePartsTracker.Data;
using BikePartsTracker.Models;
using BikePartsTracker.DTOs;

namespace BikePartsTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PartsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PartsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Parts
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BikePart>>> GetParts()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var parts = await _context.BikeParts
                .Where(p => p.UserId == userId)
                .Include(p => p.Bike)
                .ToListAsync();

            return parts;
        }

        // GET: api/Parts/bike/5
        [HttpGet("bike/{bikeId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BikePart>>> GetPartsByBike(Guid bikeId)
        {
            // Get current user from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            // Verify the bike belongs to the current user
            var bike = await _context.Bikes
                .FirstOrDefaultAsync(b => b.Id == bikeId && b.UserId == userId);

            if (bike == null)
            {
                return NotFound();
            }

            var parts = await _context.BikeParts
                .Where(p => p.BikeId == bikeId)
                .Include(p => p.Bike)
                .ToListAsync();

            return parts;
        }

        // GET: api/Parts/5
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<BikePart>> GetPart(Guid id)
        {
            // Get current user from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var part = await _context.BikeParts
                .Include(p => p.Bike)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (part == null)
            {
                return NotFound();
            }

            if (part.UserId != userId)
            {
                return Forbid();
            }

            return part;
        }

        // POST: api/Parts
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<BikePart>> PostPart([FromBody] CreatePartDto createPartDto)
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
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.BikeParts.Add(part);
            await _context.SaveChangesAsync();

            // Load the part with related entities for response
            var createdPart = await _context.BikeParts
                .Include(p => p.Bike)
                .FirstOrDefaultAsync(p => p.Id == part.Id);

            Console.WriteLine("CreatedPart: " + createdPart);

            return CreatedAtAction(nameof(GetPart), new { id = part.Id }, createdPart);
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

            // Load the existing part
            var part = await _context.BikeParts
                .Include(p => p.Bike)
                .FirstOrDefaultAsync(p => p.Id == id);

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

            // Reload the part with related entities for response
            var updatedPart = await _context.BikeParts
                .Include(p => p.Bike)
                .FirstOrDefaultAsync(p => p.Id == id);

            return Ok(new { part = updatedPart, affectedChainCycles });
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

            var part = await _context.BikeParts
                .Include(p => p.Bike)
                .FirstOrDefaultAsync(p => p.Id == id);

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
        public async Task<ActionResult<IEnumerable<BikePart>>> SearchParts([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query is required");
            }

            // Get current user from JWT token
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
                .Include(p => p.Bike)
                .ToListAsync();

            return parts;
        }

        // GET: api/Parts/type?type=Chain
        [HttpGet("type")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BikePart>>> GetPartsByType([FromQuery] string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return BadRequest("Part type is required");
            }

            // Get current user from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            // Try to parse the part type enum
            if (!Enum.TryParse<PartType>(type, ignoreCase: true, out var partType))
            {
                return BadRequest($"Invalid part type: {type}");
            }

            var parts = await _context.BikeParts
                .Where(p => p.UserId == userId && p.PartType == partType)
                .Include(p => p.Bike)
                .ToListAsync();

            return parts;
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
    }
}
