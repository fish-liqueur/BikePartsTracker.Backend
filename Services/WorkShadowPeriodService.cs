using BikePartsTracker.Data;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IWorkShadowPeriodService
    {
        Task SyncShadowPeriodsAsync(Work work);
        Task SyncShadowPeriodsForPartAsync(Guid bikePartId);
    }

    public class WorkShadowPeriodService : IWorkShadowPeriodService
    {
        private readonly AppDbContext _context;
        private readonly IUsagePeriodDistanceService _usagePeriodDistanceService;

        public WorkShadowPeriodService(AppDbContext context, IUsagePeriodDistanceService usagePeriodDistanceService)
        {
            _context = context;
            _usagePeriodDistanceService = usagePeriodDistanceService;
        }

        public async Task SyncShadowPeriodsForPartAsync(Guid bikePartId)
        {
            var works = await _context.Works
                .Where(w => w.ParentType == WorkParentType.Part &&
                            w.ParentId == bikePartId &&
                            w.TriggerType == WorkTriggerType.Distance &&
                            w.IsActive)
                .ToListAsync();

            foreach (var work in works)
            {
                await SyncShadowPeriodsAsync(work);
            }
        }

        public async Task SyncShadowPeriodsAsync(Work work)
        {
            if (work.ParentType != WorkParentType.Part || work.TriggerType != WorkTriggerType.Distance)
            {
                return;
            }

            var existingShadows = await _context.PartUsageHistories
                .Where(h => h.WorkId == work.Id && h.IsShadow)
                .ToListAsync();
            _context.PartUsageHistories.RemoveRange(existingShadows);

            var overlappingPeriods = await _context.PartUsageHistories
                .Include(h => h.BikePart)
                .Where(h => h.BikePartId == work.ParentId &&
                            !h.IsShadow &&
                            h.StartDate < work.StartDate &&
                            (h.EndDate == null || h.EndDate > work.StartDate))
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var source in overlappingPeriods)
            {
                var shadow = new PartUsageHistory
                {
                    Id = Guid.NewGuid(),
                    BikePartId = source.BikePartId,
                    BikePart = source.BikePart,
                    BikeId = source.BikeId,
                    WorkId = work.Id,
                    SourceUsagePeriodId = source.Id,
                    StartDate = work.StartDate,
                    EndDate = source.EndDate,
                    IsShadow = true,
                    Notes = "Auto-generated shadow usage period for work window.",
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await _usagePeriodDistanceService.RecalculatePeriodDistanceAsync(shadow);
                _context.PartUsageHistories.Add(shadow);
            }

            await _context.SaveChangesAsync();
        }
    }
}
