using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Exceptions;
using BikePartsTracker.Extensions;
using BikePartsTracker.Hubs;
using BikePartsTracker.Localization;
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
        private readonly IRideMutationResolver _mutationResolver;
        private readonly IRealtimeNotifier _realtimeNotifier;

        public RidesController(
            AppDbContext context,
            IRideImportService rideImportService,
            IUsagePeriodDistanceService usagePeriodDistanceService,
            IRideMutationResolver mutationResolver,
            IRealtimeNotifier realtimeNotifier)
        {
            _context = context;
            _rideImportService = rideImportService;
            _usagePeriodDistanceService = usagePeriodDistanceService;
            _mutationResolver = mutationResolver;
            _realtimeNotifier = realtimeNotifier;
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
                throw new AppException(ErrorCodes.RidesEndDateBeforeStartDate);
            }

            try
            {
                var importResult = await _rideImportService.ImportFromStravaAsync(userId, request.StartDate, request.EndDate);

                var ridesInRange = await _context.Rides
                    .Where(r => r.UserId == userId &&
                                r.StartDateLocal >= request.StartDate &&
                                r.StartDateLocal <= request.EndDate)
                    .OrderByDescending(r => r.StartDateLocal)
                    .ToListAsync();
                var rideDtos = ridesInRange.Select(MapToDto).ToList();

                await _realtimeNotifier.NotifyEntitiesAffectedAsync(userId, importResult.Affected);

                return Ok(new ImportStravaRidesResponseDto
                {
                    Inserted = importResult.Inserted,
                    Updated = importResult.Updated,
                    Rides = rideDtos,
                    Affected = importResult.Affected
                });
            }
            catch (InvalidOperationException)
            {
                // The import service throws this when Strava isn't connected or no token is available.
                throw new AppException(ErrorCodes.RidesStravaNotConnected);
            }
            // HttpRequestException (an upstream Strava failure) is left to bubble to the global handler,
            // which logs it and returns COMMON_UNEXPECTED (500) without leaking the raw message.
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
                throw AppException.NotFound();
            }

            return Ok(MapToDto(ride));
        }

        [HttpPost]
        public async Task<ActionResult<RideMutationResponseDto>> Create([FromBody] CreateRideDto dto)
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
                    throw new AppException(ErrorCodes.BikesNotFound);
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

            var affectedPartIds = await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(userId, ride.StartDateLocal, ride.StartDateLocal);

            var affected = await _mutationResolver.BuildAsync(
                userId,
                rideIds: new[] { ride.Id },
                partIds: affectedPartIds,
                bikeIds: new[] { ride.BikeId });

            await _realtimeNotifier.NotifyEntitiesAffectedAsync(userId, affected);

            var response = new RideMutationResponseDto
            {
                Ride = MapToDto(ride),
                Affected = affected
            };

            return CreatedAtAction(nameof(GetRide), new { id = ride.Id }, response);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RideMutationResponseDto>> Update(Guid id, [FromBody] UpdateRideDto dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var ride = await _context.Rides.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (ride == null)
            {
                throw AppException.NotFound();
            }

            var oldBikeId = ride.BikeId;

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
                        throw new AppException(ErrorCodes.BikesNotFound);
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

            IReadOnlyCollection<Guid> affectedPartIds = Array.Empty<Guid>();
            if (needsRecalc)
            {
                affectedPartIds = await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(userId, startMin, startMax);
            }

            var affected = await _mutationResolver.BuildAsync(
                userId,
                rideIds: new[] { ride.Id },
                partIds: affectedPartIds,
                bikeIds: new[] { oldBikeId, ride.BikeId });

            await _realtimeNotifier.NotifyEntitiesAffectedAsync(userId, affected);

            return Ok(new RideMutationResponseDto
            {
                Ride = MapToDto(ride),
                Affected = affected
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<RideMutationResultDto>> Delete(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var ride = await _context.Rides.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (ride == null)
            {
                throw AppException.NotFound();
            }

            var startDate = ride.StartDateLocal;
            var bikeId = ride.BikeId;
            var rideId = ride.Id;

            _context.Rides.Remove(ride);
            await _context.SaveChangesAsync();

            var affectedPartIds = await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(userId, startDate, startDate);

            var affected = await _mutationResolver.BuildAsync(
                userId,
                rideIds: new[] { rideId },
                partIds: affectedPartIds,
                bikeIds: new[] { bikeId });

            await _realtimeNotifier.NotifyEntitiesAffectedAsync(userId, affected);

            return Ok(affected);
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
