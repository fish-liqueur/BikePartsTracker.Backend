using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IRideImportService
    {
        Task<(int inserted, int updated)> ImportFromStravaAsync(Guid userId, DateTime startDate, DateTime endDate);
    }

    public class RideImportService : IRideImportService
    {
        private readonly AppDbContext _context;
        private readonly IStravaService _stravaService;
        private readonly IStravaIntegrationService _stravaIntegrationService;
        private readonly IUsagePeriodDistanceService _usagePeriodDistanceService;

        public RideImportService(
            AppDbContext context,
            IStravaService stravaService,
            IStravaIntegrationService stravaIntegrationService,
            IUsagePeriodDistanceService usagePeriodDistanceService)
        {
            _context = context;
            _stravaService = stravaService;
            _stravaIntegrationService = stravaIntegrationService;
            _usagePeriodDistanceService = usagePeriodDistanceService;
        }

        public async Task<(int inserted, int updated)> ImportFromStravaAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
            {
                throw new ArgumentException("EndDate must be greater than or equal to StartDate.");
            }

            var integration = await _stravaIntegrationService.GetUserStravaIntegrationAsync(userId)
                ?? throw new InvalidOperationException("Strava integration not found.");

            var accessToken = await _stravaIntegrationService.EnsureValidAccessTokenAsync(integration);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Unable to obtain a valid Strava access token.");
            }

            var before = new DateTimeOffset(endDate).ToUnixTimeSeconds();
            var after = new DateTimeOffset(startDate).ToUnixTimeSeconds();

            var importedActivities = new List<StravaActivityDto>();
            const int pageSize = 100;
            var page = 1;

            while (true)
            {
                var pageActivities = await _stravaService.GetActivitiesAsync(
                    accessToken,
                    before: before,
                    after: after,
                    page: page,
                    perPage: pageSize);

                if (pageActivities.Count == 0)
                {
                    break;
                }

                importedActivities.AddRange(pageActivities);
                if (pageActivities.Count < pageSize)
                {
                    break;
                }

                page++;
            }

            var existingRides = await _context.Rides
                .Where(r => r.UserId == userId && r.StravaActivityId != null)
                .ToDictionaryAsync(r => r.StravaActivityId!.Value, r => r);

            var bikes = await _context.Bikes
                .Where(b => b.UserId == userId && b.StravaBikeId != null)
                .ToDictionaryAsync(b => b.StravaBikeId!, b => b.Id);

            var inserted = 0;
            var updated = 0;
            var now = DateTime.UtcNow;

            foreach (var activity in importedActivities)
            {
                bikes.TryGetValue(activity.GearId ?? string.Empty, out var bikeId);
                Guid? mappedBikeId = bikeId == Guid.Empty ? null : bikeId;

                if (!existingRides.TryGetValue(activity.Id, out var existingRide))
                {
                    var ride = new Ride
                    {
                        Id = Guid.NewGuid(),
                        StravaActivityId = activity.Id,
                        UserId = userId,
                        User = null!,
                        BikeId = mappedBikeId,
                        Name = activity.Name,
                        Description = activity.Description,
                        Type = activity.Type,
                        GearId = activity.GearId,
                        Distance = activity.Distance,
                        UserDistance = activity.Distance,
                        StartDateLocal = activity.StartDateLocal,
                        IsActive = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    _context.Rides.Add(ride);
                    inserted++;
                    continue;
                }

                var oldDistance = existingRide.Distance;
                var oldUserDistance = existingRide.UserDistance;

                existingRide.Name = activity.Name;
                existingRide.Description = activity.Description;
                existingRide.Type = activity.Type;
                existingRide.GearId = activity.GearId;
                existingRide.BikeId = mappedBikeId;
                existingRide.Distance = activity.Distance;
                existingRide.StartDateLocal = activity.StartDateLocal;
                existingRide.UpdatedAt = now;

                if (activity.Distance == 0)
                {
                    existingRide.UserDistance = oldUserDistance;
                }
                else if (oldDistance <= 0)
                {
                    existingRide.UserDistance = activity.Distance;
                }
                else
                {
                    var ratio = oldUserDistance / oldDistance;
                    existingRide.UserDistance = activity.Distance * ratio;
                }

                updated++;
            }

            await _context.SaveChangesAsync();

            await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(userId, startDate, endDate);
            return (inserted, updated);
        }
    }
}
