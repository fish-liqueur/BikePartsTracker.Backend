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
    public class RidesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IRideImportService _rideImportService;
        private readonly IUsagePeriodDistanceService _usagePeriodDistanceService;

        public RidesController(
            AppDbContext context,
            IRideImportService rideImportService,
            IUsagePeriodDistanceService usagePeriodDistanceService)
        {
            _context = context;
            _rideImportService = rideImportService;
            _usagePeriodDistanceService = usagePeriodDistanceService;
        }

        [HttpPost("import/strava")]
        public async Task<ActionResult<ImportStravaRidesResponseDto>> ImportFromStrava([FromBody] ImportStravaRidesRequestDto request)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            if (request.EndDate < request.StartDate)
            {
                return BadRequest(new { message = "EndDate must be greater than or equal to StartDate." });
            }

            try
            {
                var (inserted, updated) = await _rideImportService.ImportFromStravaAsync(userId, request.StartDate, request.EndDate);

                var ridesInRange = await _context.Rides
                    .Where(r => r.UserId == userId &&
                                r.StartDateLocal >= request.StartDate &&
                                r.StartDateLocal <= request.EndDate)
                    .OrderByDescending(r => r.StartDateLocal)
                    .ToListAsync();
                var rideDtos = ridesInRange.Select(MapToDto).ToList();

                return Ok(new ImportStravaRidesResponseDto
                {
                    Inserted = inserted,
                    Updated = updated,
                    Rides = rideDtos
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(500, new { message = $"Failed to fetch Strava activities: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RideDto>>> GetRides([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var query = _context.Rides.Where(r => r.UserId == userId);

            if (startDate.HasValue)
            {
                query = query.Where(r => r.StartDateLocal >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.StartDateLocal <= endDate.Value);
            }

            var rides = await query
                .OrderByDescending(r => r.StartDateLocal)
                .ToListAsync();

            return Ok(rides.Select(MapToDto));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RideDto>> GetRide(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var ride = await _context.Rides.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (ride == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(ride));
        }

        [HttpPost]
        public async Task<ActionResult<RideDto>> Create([FromBody] CreateRideDto dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            if (dto.BikeId.HasValue)
            {
                var bikeOk = await _context.Bikes.AnyAsync(b => b.Id == dto.BikeId.Value && b.UserId == userId);
                if (!bikeOk)
                {
                    return BadRequest(new { message = "Bike not found or does not belong to the current user." });
                }
            }

            var type = string.IsNullOrWhiteSpace(dto.Type) ? "Ride" : dto.Type;
            var now = DateTime.UtcNow;

            var ride = new Ride
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = null!,
                BikeId = dto.BikeId,
                Name = dto.Name,
                Description = dto.Description,
                Type = type,
                GearId = dto.GearId,
                RecordedDistance = 0,
                Distance = dto.Distance,
                StartDateLocal = dto.StartDateLocal,
                IsActive = dto.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Rides.Add(ride);
            await _context.SaveChangesAsync();

            await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(userId, ride.StartDateLocal, ride.StartDateLocal);

            return CreatedAtAction(nameof(GetRide), new { id = ride.Id }, MapToDto(ride));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RideDto>> Update(Guid id, [FromBody] UpdateRideDto dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var ride = await _context.Rides.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (ride == null)
            {
                return NotFound();
            }

            if (dto.BikeId is { } nextBike)
            {
                if (nextBike == Guid.Empty)
                {
                    ride.BikeId = null;
                }
                else
                {
                    var bikeOk = await _context.Bikes.AnyAsync(b => b.Id == nextBike && b.UserId == userId);
                    if (!bikeOk)
                    {
                        return BadRequest(new { message = "Bike not found or does not belong to the current user." });
                    }

                    ride.BikeId = nextBike;
                }
            }

            if (dto.Name != null)
            {
                ride.Name = dto.Name;
            }

            if (dto.Description != null)
            {
                ride.Description = dto.Description;
            }

            if (dto.Type != null)
            {
                ride.Type = dto.Type;
            }

            if (dto.GearId != null)
            {
                ride.GearId = string.IsNullOrWhiteSpace(dto.GearId) ? null : dto.GearId;
            }

            var oldStart = ride.StartDateLocal;

            if (dto.Distance.HasValue)
            {
                ride.Distance = dto.Distance.Value;
            }

            if (dto.StartDateLocal.HasValue)
            {
                ride.StartDateLocal = dto.StartDateLocal.Value;
            }

            if (dto.IsActive.HasValue)
            {
                ride.IsActive = dto.IsActive.Value;
            }

            var startMin = oldStart <= ride.StartDateLocal ? oldStart : ride.StartDateLocal;
            var startMax = oldStart >= ride.StartDateLocal ? oldStart : ride.StartDateLocal;
            var needsRecalc = dto.Distance.HasValue
                || dto.StartDateLocal.HasValue
                || dto.IsActive.HasValue
                || dto.BikeId is not null;

            ride.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (needsRecalc)
            {
                await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(userId, startMin, startMax);
            }

            return Ok(MapToDto(ride));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var ride = await _context.Rides.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (ride == null)
            {
                return NotFound();
            }

            var startDate = ride.StartDateLocal;
            _context.Rides.Remove(ride);
            await _context.SaveChangesAsync();

            await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(userId, startDate, startDate);

            return NoContent();
        }

        private static RideDto MapToDto(Ride r) => new()
        {
            Id = r.Id,
            StravaActivityId = r.StravaActivityId,
            BikeId = r.BikeId,
            Name = r.Name,
            Description = r.Description,
            Type = r.Type,
            GearId = r.GearId,
            Distance = r.Distance,
            RecordedDistance = r.RecordedDistance,
            IsActive = r.IsActive,
            StartDateLocal = r.StartDateLocal
        };
    }
}
