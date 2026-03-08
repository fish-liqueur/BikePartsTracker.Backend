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
                .ToListAsync();
        }

        // GET: api/Bikes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Bike>> GetBike(Guid id)
        {
            var bike = await _context.Bikes
                .Include(b => b.User)
                .Include(b => b.Parts)
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
            // Get current user from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            // Load the user to satisfy the required navigation property
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
                ChainsInCycle = createBikeDto.ChainsInCycle ?? new List<Guid?>(),
                ActiveChainId = createBikeDto.ActiveChainId,
                ChainCycleInterval = createBikeDto.ChainCycleInterval,
                ChainsCycleLength = createBikeDto.ChainsCycleLength,
                CreatedAt = createBikeDto.CreatedAt ?? now,
                UpdatedAt = createBikeDto.UpdatedAt ?? now
            };
            
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

            // Get current user from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            // Load the existing bike
            var bike = await _context.Bikes
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bike == null)
            {
                return NotFound();
            }

            // Verify the bike belongs to the current user
            if (bike.UserId != userId)
            {
                return Forbid();
            }

            // Update only the fields provided in the DTO
            if (updateBikeDto.Name != null)
            {
                bike.Name = updateBikeDto.Name;
            }

            if (updateBikeDto.Description != null)
            {
                bike.Description = updateBikeDto.Description;
            }

            if (updateBikeDto.Type.HasValue)
            {
                bike.Type = updateBikeDto.Type.Value.ToString();
            }

            if (updateBikeDto.TotalDistance.HasValue)
            {
                bike.TotalDistance = updateBikeDto.TotalDistance.Value;
            }

            if (updateBikeDto.StravaDistance.HasValue)
            {
                bike.StravaDistance = updateBikeDto.StravaDistance.Value;
            }

            if (updateBikeDto.ChainsCycleLength.HasValue)
            {
                bike.ChainsCycleLength = updateBikeDto.ChainsCycleLength.Value;
            }

            if (updateBikeDto.ChainCycleInterval.HasValue)
            {
                bike.ChainCycleInterval = updateBikeDto.ChainCycleInterval.Value;
            }

            // Handle ChainsInCycle - update if provided (null means clear, array means set)
            bool chainsInCycleUpdated = false;
            if (updateBikeDto.ChainsInCycle != null)
            {
                // Convert string IDs to Guids
                var guidList = new List<Guid?>();
                foreach (var chainIdStr in updateBikeDto.ChainsInCycle)
                {
                    if (chainIdStr == null)
                    {
                        guidList.Add(null);
                    }
                    else if (Guid.TryParse(chainIdStr, out var guid))
                    {
                        guidList.Add(guid);
                    }
                    else
                    {
                        ModelState.AddModelError(nameof(UpdateBikeDto.ChainsInCycle), 
                            $"Invalid Guid format in ChainsInCycle: '{chainIdStr}'. Expected a valid Guid.");
                        return BadRequest(ModelState);
                    }
                }
                // Use the ChainsInCycle property - it will automatically serialize to JSON
                bike.ChainsInCycle = guidList;
                chainsInCycleUpdated = true;
            }

            // Handle ActiveChainId - update if provided
            // If ChainsInCycle is being updated, also update ActiveChainId (allows clearing)
            // Otherwise, only update if it has a value
            if (chainsInCycleUpdated || !string.IsNullOrEmpty(updateBikeDto.ActiveChainId))
            {
                if (string.IsNullOrEmpty(updateBikeDto.ActiveChainId))
                {
                    bike.ActiveChainId = null;
                }
                else if (Guid.TryParse(updateBikeDto.ActiveChainId, out var activeChainGuid))
                {
                    bike.ActiveChainId = activeChainGuid;
                }
                else
                {
                    ModelState.AddModelError(nameof(UpdateBikeDto.ActiveChainId), 
                        $"Invalid Guid format for ActiveChainId: '{updateBikeDto.ActiveChainId}'. Expected a valid Guid.");
                    return BadRequest(ModelState);
                }
            }

            if (updateBikeDto.StravaId != null)
            {
                bike.StravaBikeId = updateBikeDto.StravaId;
            }

            if (updateBikeDto.IsActive.HasValue)
            {
                bike.IsActive = updateBikeDto.IsActive.Value;
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
        /// <param name="request">Bikes to sync</param>
        /// <returns>Success response</returns>
        /// <response code="200">Bikes synced successfully</response>
        /// <response code="400">Invalid request data or validation error</response>
        /// <response code="401">User not authenticated</response>
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
                // Get current user from JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized();
                }

                // Validate: check for duplicate Strava bike IDs
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

                // Get all existing bikes for this user
                var existingBikes = await _context.Bikes
                    .Where(b => b.UserId == userId)
                    .ToListAsync();

                // Get user from database (needed for new bikes)
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Process each bike in the request
                foreach (var bikeDto in request.Bikes)
                {
                    Bike? bike = null;

                    // Try to find existing bike by internal ID
                    if (bikeDto.Id.HasValue)
                    {
                        bike = existingBikes.FirstOrDefault(b => b.Id == bikeDto.Id.Value);
                    }

                    // If not found and has Strava ID, try to find by Strava ID
                    if (bike == null && !string.IsNullOrEmpty(bikeDto.StravaBikeId))
                    {
                        bike = existingBikes.FirstOrDefault(b => b.StravaBikeId == bikeDto.StravaBikeId);
                    }

                    if (bike != null)
                    {
                        // Update existing bike
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
                        // Create new bike
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
                            ChainsInCycleJson = "[]",
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
