using BikePartsTracker.Data;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IPartUsageTrackingService
    {
        Task OpenUsagePeriodAsync(BikePart part, Guid bikeId, DateTime startDate);
        Task CloseOpenUsagePeriodsAsync(Guid bikePartId, DateTime endDate);
    }

    public class PartUsageTrackingService : IPartUsageTrackingService
    {
        private readonly AppDbContext _context;
        private readonly IUsagePeriodDistanceService _distanceService;
        private readonly IWorkShadowPeriodService _shadowService;

        public PartUsageTrackingService(
            AppDbContext context,
            IUsagePeriodDistanceService distanceService,
            IWorkShadowPeriodService shadowService)
        {
            _context = context;
            _distanceService = distanceService;
            _shadowService = shadowService;
        }

        public async Task OpenUsagePeriodAsync(BikePart part, Guid bikeId, DateTime startDate)
        {
            var alreadyOpen = await _context.PartUsageHistories
                .AnyAsync(h => h.BikePartId == part.Id &&
                               h.BikeId == bikeId &&
                               h.EndDate == null &&
                               !h.IsShadow);
            if (alreadyOpen)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var period = new PartUsageHistory
            {
                Id = Guid.NewGuid(),
                BikePartId = part.Id,
                BikePart = part,
                BikeId = bikeId,
                StartDate = startDate,
                EndDate = null,
                IsShadow = false,
                Distance = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _distanceService.RecalculatePeriodDistanceAsync(period);
            _context.PartUsageHistories.Add(period);
            await _context.SaveChangesAsync();

            await _shadowService.SyncShadowPeriodsForPartAsync(part.Id);
        }

        public async Task CloseOpenUsagePeriodsAsync(Guid bikePartId, DateTime endDate)
        {
            var openPeriods = await _context.PartUsageHistories
                .Include(h => h.BikePart)
                .Where(h => h.BikePartId == bikePartId &&
                            h.EndDate == null &&
                            !h.IsShadow)
                .ToListAsync();

            if (openPeriods.Count == 0)
            {
                return;
            }

            foreach (var period in openPeriods)
            {
                period.EndDate = period.StartDate <= endDate ? endDate : period.StartDate;
                await _distanceService.RecalculatePeriodDistanceAsync(period);
            }

            await _context.SaveChangesAsync();
            await _shadowService.SyncShadowPeriodsForPartAsync(bikePartId);
        }
    }
}
