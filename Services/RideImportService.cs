using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public class RideImportResult
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public RideMutationResultDto Affected { get; set; } = new();

        /// <summary>
        /// Activity calendar day for webhook single-activity upserts (used to expand auto-import watermark).
        /// </summary>
        public DateTime? ActivityDate { get; set; }
    }

    public interface IRideImportService
    {
        Task<RideImportResult> ImportFromStravaAsync(Guid userId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Fetch a single Strava activity and upsert it (webhook create/update path).
        /// Returns null when the activity cannot be fetched (e.g. gone / no token).
        /// </summary>
        Task<RideImportResult?> UpsertStravaActivityAsync(Guid userId, long stravaActivityId);

        /// <summary>
        /// Delete the local ride for a Strava activity id (same semantics as manual ride delete).
        /// Returns empty affected ids when no matching ride exists.
        /// </summary>
        Task<RideMutationResultDto> DeleteByStravaActivityIdAsync(Guid userId, long stravaActivityId);
    }

    public class RideImportService : IRideImportService
    {
        private readonly AppDbContext _context;
        private readonly IStravaService _stravaService;
        private readonly IStravaIntegrationService _stravaIntegrationService;
        private readonly IUsagePeriodDistanceService _usagePeriodDistanceService;
        private readonly IRideMutationResolver _mutationResolver;

        public RideImportService(
            AppDbContext context,
            IStravaService stravaService,
            IStravaIntegrationService stravaIntegrationService,
            IUsagePeriodDistanceService usagePeriodDistanceService,
            IRideMutationResolver mutationResolver)
        {
            _context = context;
            _stravaService = stravaService;
            _stravaIntegrationService = stravaIntegrationService;
            _usagePeriodDistanceService = usagePeriodDistanceService;
            _mutationResolver = mutationResolver;
        }

        public async Task<RideImportResult> ImportFromStravaAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
            {
                throw new ArgumentException("EndDate must be greater than or equal to StartDate.");
            }

            var accessToken = await RequireAccessTokenAsync(userId);

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

            return await UpsertActivitiesAsync(userId, importedActivities, startDate, endDate);
        }

        public async Task<RideImportResult?> UpsertStravaActivityAsync(Guid userId, long stravaActivityId)
        {
            var accessToken = await RequireAccessTokenAsync(userId);
            var activity = await _stravaService.GetActivityAsync(accessToken, stravaActivityId);
            if (activity == null)
            {
                return null;
            }

            var windowStart = activity.StartDateLocal;
            var windowEnd = activity.StartDateLocal;
            var result = await UpsertActivitiesAsync(userId, new[] { activity }, windowStart, windowEnd);
            result.ActivityDate = activity.StartDateLocal.Date;
            return result;
        }

        public async Task<RideMutationResultDto> DeleteByStravaActivityIdAsync(Guid userId, long stravaActivityId)
        {
            var ride = await _context.Rides
                .FirstOrDefaultAsync(r => r.UserId == userId && r.StravaActivityId == stravaActivityId);

            if (ride == null)
            {
                return new RideMutationResultDto();
            }

            var startDate = ride.StartDateLocal;
            var bikeId = ride.BikeId;
            var rideId = ride.Id;

            _context.Rides.Remove(ride);
            await _context.SaveChangesAsync();

            var affectedPartIds = await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(
                userId, startDate, startDate);

            return await _mutationResolver.BuildAsync(
                userId,
                rideIds: new[] { rideId },
                partIds: affectedPartIds,
                bikeIds: new[] { bikeId });
        }

        private async Task<string> RequireAccessTokenAsync(Guid userId)
        {
            var integration = await _stravaIntegrationService.GetUserStravaIntegrationAsync(userId)
                ?? throw new InvalidOperationException("Strava integration not found.");

            var accessToken = await _stravaIntegrationService.EnsureValidAccessTokenAsync(integration);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Unable to obtain a valid Strava access token.");
            }

            return accessToken;
        }

        private async Task<RideImportResult> UpsertActivitiesAsync(
            Guid userId,
            IReadOnlyList<StravaActivityDto> importedActivities,
            DateTime recalcStart,
            DateTime recalcEnd)
        {
            var existingRides = await _context.Rides
                .Where(r => r.UserId == userId && r.StravaActivityId != null)
                .ToDictionaryAsync(r => r.StravaActivityId!.Value, r => r);

            var bikes = await _context.Bikes
                .Where(b => b.UserId == userId && b.StravaBikeId != null)
                .ToDictionaryAsync(b => b.StravaBikeId!, b => b.Id);

            var inserted = 0;
            var updated = 0;
            var now = DateTime.UtcNow;
            var touchedRideIds = new List<Guid>();
            var touchedBikeIds = new List<Guid?>();

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
                        RecordedDistance = activity.Distance,
                        Distance = activity.Distance,
                        StartDateLocal = activity.StartDateLocal,
                        IsActive = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    _context.Rides.Add(ride);
                    touchedRideIds.Add(ride.Id);
                    touchedBikeIds.Add(mappedBikeId);
                    inserted++;
                    continue;
                }

                var oldRecordedDistance = existingRide.RecordedDistance;
                var oldDistance = existingRide.Distance;
                var oldBikeId = existingRide.BikeId;

                existingRide.Name = activity.Name;
                existingRide.Description = activity.Description;
                existingRide.Type = activity.Type;
                existingRide.GearId = activity.GearId;
                existingRide.BikeId = mappedBikeId;
                existingRide.RecordedDistance = activity.Distance;
                existingRide.StartDateLocal = activity.StartDateLocal;
                existingRide.UpdatedAt = now;

                // Preserve manual distance corrections (same rules as range import).
                if (activity.Distance == 0)
                {
                    existingRide.Distance = oldDistance;
                }
                else if (oldRecordedDistance <= 0)
                {
                    existingRide.Distance = activity.Distance;
                }
                else
                {
                    var ratio = oldDistance / oldRecordedDistance;
                    existingRide.Distance = activity.Distance * ratio;
                }

                touchedRideIds.Add(existingRide.Id);
                touchedBikeIds.Add(oldBikeId);
                touchedBikeIds.Add(mappedBikeId);
                updated++;
            }

            await _context.SaveChangesAsync();

            var affectedPartIds = await _usagePeriodDistanceService.RecalculateOverlappingPeriodsAsync(
                userId, recalcStart, recalcEnd);

            var affected = await _mutationResolver.BuildAsync(
                userId,
                rideIds: touchedRideIds,
                partIds: affectedPartIds,
                bikeIds: touchedBikeIds);

            return new RideImportResult
            {
                Inserted = inserted,
                Updated = updated,
                Affected = affected
            };
        }
    }
}
