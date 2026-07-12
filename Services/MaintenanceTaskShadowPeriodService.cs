using BikePartsTracker.Data;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IMaintenanceTaskShadowPeriodService
    {
        Task SyncShadowPeriodsAsync(MaintenanceTask maintenanceTask);
        Task SyncShadowPeriodsForPartAsync(Guid bikePartId);
    }

    public class MaintenanceTaskShadowPeriodService : IMaintenanceTaskShadowPeriodService
    {
        private readonly AppDbContext _context;
        private readonly IUsagePeriodDistanceService _usagePeriodDistanceService;

        public MaintenanceTaskShadowPeriodService(AppDbContext context, IUsagePeriodDistanceService usagePeriodDistanceService)
        {
            _context = context;
            _usagePeriodDistanceService = usagePeriodDistanceService;
        }

        public async Task SyncShadowPeriodsForPartAsync(Guid bikePartId)
        {
            var maintenanceTasks = await _context.MaintenanceTasks
                .Where(w => w.ParentType == MaintenanceTaskParentType.Part &&
                            w.ParentId == bikePartId &&
                            w.TriggerType == MaintenanceTaskTriggerType.Distance &&
                            w.IsActive)
                .ToListAsync();

            foreach (var maintenanceTask in maintenanceTasks)
            {
                await SyncShadowPeriodsAsync(maintenanceTask);
            }
        }

        public async Task SyncShadowPeriodsAsync(MaintenanceTask maintenanceTask)
        {
            if (maintenanceTask.ParentType != MaintenanceTaskParentType.Part || maintenanceTask.TriggerType != MaintenanceTaskTriggerType.Distance)
            {
                return;
            }

            var existingShadows = await _context.PartUsageHistories
                .Where(h => h.MaintenanceTaskId == maintenanceTask.Id && h.IsShadow)
                .ToListAsync();
            _context.PartUsageHistories.RemoveRange(existingShadows);

            var overlappingPeriods = await _context.PartUsageHistories
                .Include(h => h.BikePart)
                .Where(h => h.BikePartId == maintenanceTask.ParentId &&
                            !h.IsShadow &&
                            h.StartDate < maintenanceTask.StartDate &&
                            (h.EndDate == null || h.EndDate > maintenanceTask.StartDate))
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
                    MaintenanceTaskId = maintenanceTask.Id,
                    SourceUsagePeriodId = source.Id,
                    StartDate = maintenanceTask.StartDate,
                    EndDate = source.EndDate,
                    IsShadow = true,
                    Notes = "Auto-generated shadow usage period for maintenance task window.",
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
