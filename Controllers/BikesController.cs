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
    public class BikesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BikesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Bikes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Bike>>> GetBikes()
        {
            return await _context.Bikes
                .Include(b => b.User)
                .Include(b => b.Parts)
                .Include(b => b.ChainCycles)
                .ToListAsync();
        }

        // GET: api/Bikes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Bike>> GetBike(Guid id)
        {
            var bike = await _context.Bikes
                .Include(b => b.User)
                .Include(b => b.Parts)
                .Include(b => b.ChainCycles)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bike == null)
            {
                return NotFound();
            }

            return bike;
        }

        // POST: api/Bikes
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Bike>> PostBike(CreateBikeDto createBikeDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return BadRequest("User not found");
            }

            var now = DateTime.UtcNow;
            var bike = new Bike
            {
                Id = Guid.NewGuid(),
                StravaBikeId = createBikeDto.StravaId,
                UserId = userId,
                User = user,
                Name = createBikeDto.Name,
                Description = createBikeDto.Description ?? string.Empty,
                Type = createBikeDto.Type?.ToString() ?? string.Empty,
                TotalDistance = createBikeDto.TotalDistance ?? 0.0,
                StravaDistance = createBikeDto.StravaDistance ?? 0.0,
                IsActive = createBikeDto.IsActive ?? true,
                CreatedAt = createBikeDto.CreatedAt ?? now,
                UpdatedAt = createBikeDto.UpdatedAt ?? now
            };

            if (createBikeDto.ChainCycles != null)
            {
                foreach (var cycleDto in createBikeDto.ChainCycles)
                {
                    bike.ChainCycles.Add(new ChainCycle
                    {
                        Id = Guid.NewGuid(),
                        Chains = cycleDto.Chains,
                        ActiveChainId = cycleDto.ActiveChainId,
                        IntervalKm = cycleDto.IntervalKm,
                        CycleLength = cycleDto.CycleLength,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            _context.Bikes.Add(bike);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBike), new { id = bike.Id }, bike);
        }

        // PUT: api/Bikes/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<Bike>> PutBike(Guid id, [FromBody] UpdateBikeDto updateBikeDto)
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

            var bike = await _context.Bikes
                .Include(b => b.User)
                .Include(b => b.ChainCycles)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bike == null)
            {
                return NotFound();
            }

            if (bike.UserId != userId)
            {
                return Forbid();
            }

            if (updateBikeDto.Name != null)
                bike.Name = updateBikeDto.Name;

            if (updateBikeDto.Description != null)
                bike.Description = updateBikeDto.Description;

            if (updateBikeDto.Type.HasValue)
                bike.Type = updateBikeDto.Type.Value.ToString();

            if (updateBikeDto.TotalDistance.HasValue)
                bike.TotalDistance = updateBikeDto.TotalDistance.Value;

            if (updateBikeDto.StravaDistance.HasValue)
                bike.StravaDistance = updateBikeDto.StravaDistance.Value;

            if (updateBikeDto.StravaId != null)
                bike.StravaBikeId = updateBikeDto.StravaId;

            if (updateBikeDto.IsActive.HasValue)
                bike.IsActive = updateBikeDto.IsActive.Value;

            // null = no change; empty array = clear all; non-empty array = full replacement
            if (updateBikeDto.ChainCycles != null)
            {
                _context.ChainCycles.RemoveRange(bike.ChainCycles);
                bike.ChainCycles.Clear();

                var now = DateTime.UtcNow;
                foreach (var cycleDto in updateBikeDto.ChainCycles)
                {
                    bike.ChainCycles.Add(new ChainCycle
                    {
                        Id = cycleDto.Id ?? Guid.NewGuid(),
                        Chains = cycleDto.Chains ?? new List<Guid>(),
                        ActiveChainId = cycleDto.ActiveChainId,
                        IntervalKm = cycleDto.IntervalKm,
                        CycleLength = cycleDto.CycleLength,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            bike.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BikeExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            await _context.Entry(bike).Collection(b => b.Parts).LoadAsync();

            return Ok(bike);
        }

        // DELETE: api/Bikes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBike(Guid id)
        {
            var bike = await _context.Bikes.FindAsync(id);
            if (bike == null)
            {
                return NotFound();
            }

            _context.Bikes.Remove(bike);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Sync bikes (merge/update bikes from frontend, typically after Strava import)
        /// </summary>
        [HttpPost("sync")]
        [Authorize]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult> SyncBikes([FromBody] SyncBikesRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request data" });
            }

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized();
                }

                var stravaBikeIds = request.Bikes
                    .Where(b => !string.IsNullOrEmpty(b.StravaBikeId))
                    .Select(b => b.StravaBikeId!)
                    .ToList();

                var duplicateStravaIds = stravaBikeIds
                    .GroupBy(id => id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateStravaIds.Any())
                {
                    return BadRequest(new { message = $"Duplicate Strava bike IDs found: {string.Join(", ", duplicateStravaIds)}" });
                }

                var existingBikes = await _context.Bikes
                    .Where(b => b.UserId == userId)
                    .ToListAsync();

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Unauthorized();
                }

                foreach (var bikeDto in request.Bikes)
                {
                    Bike? bike = null;

                    if (bikeDto.Id.HasValue)
                    {
                        bike = existingBikes.FirstOrDefault(b => b.Id == bikeDto.Id.Value);
                    }

                    if (bike == null && !string.IsNullOrEmpty(bikeDto.StravaBikeId))
                    {
                        bike = existingBikes.FirstOrDefault(b => b.StravaBikeId == bikeDto.StravaBikeId);
                    }

                    if (bike != null)
                    {
                        bike.Name = bikeDto.Name;
                        bike.Type = bikeDto.Type?.ToString() ?? string.Empty;
                        bike.TotalDistance = bikeDto.TotalDistance;
                        bike.StravaDistance = bikeDto.StravaDistance;
                        bike.StravaBikeId = bikeDto.StravaBikeId;
                        bike.IsActive = bikeDto.IsActive;
                        bike.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        var now = DateTime.UtcNow;
                        bike = new Bike
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            User = user,
                            Name = bikeDto.Name,
                            Description = string.Empty,
                            Type = bikeDto.Type?.ToString() ?? string.Empty,
                            TotalDistance = bikeDto.TotalDistance,
                            StravaDistance = bikeDto.StravaDistance,
                            StravaBikeId = bikeDto.StravaBikeId,
                            IsActive = bikeDto.IsActive,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _context.Bikes.Add(bike);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Bikes synced successfully" });
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                return StatusCode(500, new { message = $"Database error: {innerMessage}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
            }
        }

        private bool BikeExists(Guid id)
        {
            return _context.Bikes.Any(e => e.Id == id);
        }
    }
}
