using BikePartsTracker.Data;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IUsagePeriodDistanceService
    {
        Task RecalculatePeriodDistanceAsync(PartUsageHistory period);

        /// <summary>
        /// Recomputes cached <see cref="PartUsageHistory.Distance"/> for every period of the user
        /// that intersects the given window. Returns the distinct ids of parts whose periods were
        /// touched, so callers can include them in mutation invalidation responses.
        /// </summary>
        Task<IReadOnlyCollection<Guid>> RecalculateOverlappingPeriodsAsync(Guid userId, DateTime startDate, DateTime endDate);
    }

    public class UsagePeriodDistanceService : IUsagePeriodDistanceService
    {
        private readonly AppDbContext _context;

        public UsagePeriodDistanceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task RecalculatePeriodDistanceAsync(PartUsageHistory period)
        {
            if (!period.BikeId.HasValue)
            {
                period.Distance = 0;
                period.UpdatedAt = DateTime.UtcNow;
                return;
            }

            var userId = period.BikePart?.UserId;
            if (!userId.HasValue || userId == Guid.Empty)
            {
                userId = await _context.BikeParts
                    .Where(p => p.Id == period.BikePartId)
                    .Select(p => p.UserId)
                    .FirstOrDefaultAsync();
            }

            if (!userId.HasValue || userId == Guid.Empty)
            {
                period.Distance = 0;
                period.UpdatedAt = DateTime.UtcNow;
                return;
            }

            var periodEnd = period.EndDate ?? DateTime.MaxValue;
            var distance = await _context.Rides
                .Where(r => r.UserId == userId.Value &&
                            r.IsActive &&
                            r.BikeId == period.BikeId &&
                            r.StartDateLocal >= period.StartDate &&
                            r.StartDateLocal <= periodEnd)
                .SumAsync(r => (double?)r.Distance) ?? 0.0;

            period.Distance = distance;
            period.UpdatedAt = DateTime.UtcNow;
        }

        public async Task<IReadOnlyCollection<Guid>> RecalculateOverlappingPeriodsAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            var overlappingPeriods = await _context.PartUsageHistories
                .Include(p => p.BikePart)
                .Where(p => p.BikePart.UserId == userId &&
                            p.StartDate <= endDate &&
                            (p.EndDate == null || p.EndDate >= startDate))
                .ToListAsync();

            foreach (var period in overlappingPeriods)
            {
                await RecalculatePeriodDistanceAsync(period);
            }

            await _context.SaveChangesAsync();

            return overlappingPeriods
                .Select(p => p.BikePartId)
                .Distinct()
                .ToList();
        }
    }
}
