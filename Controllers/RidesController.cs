using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Extensions;
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

        public RidesController(AppDbContext context, IRideImportService rideImportService)
        {
            _context = context;
            _rideImportService = rideImportService;
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
                    .Select(r => new RideDto
                    {
                        Id = r.Id,
                        StravaActivityId = r.StravaActivityId,
                        BikeId = r.BikeId,
                        Name = r.Name,
                        Description = r.Description,
                        Type = r.Type,
                        GearId = r.GearId,
                        Distance = r.Distance,
                        UserDistance = r.UserDistance,
                        IsActive = r.IsActive,
                        StartDateLocal = r.StartDateLocal
                    })
                    .ToListAsync();

                return Ok(new ImportStravaRidesResponseDto
                {
                    Inserted = inserted,
                    Updated = updated,
                    Rides = ridesInRange
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
                .Select(r => new RideDto
                {
                    Id = r.Id,
                    StravaActivityId = r.StravaActivityId,
                    BikeId = r.BikeId,
                    Name = r.Name,
                    Description = r.Description,
                    Type = r.Type,
                    GearId = r.GearId,
                    Distance = r.Distance,
                    UserDistance = r.UserDistance,
                    IsActive = r.IsActive,
                    StartDateLocal = r.StartDateLocal
                })
                .ToListAsync();

            return Ok(rides);
        }

    }
}
