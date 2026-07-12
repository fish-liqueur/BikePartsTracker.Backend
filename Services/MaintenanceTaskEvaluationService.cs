using BikePartsTracker.Data;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IMaintenanceTaskEvaluationService
    {
        Task<double> GetConsumedValueAsync(MaintenanceTask maintenanceTask);
        Task<bool> NeedsAttentionAsync(MaintenanceTask maintenanceTask);
    }

    public class MaintenanceTaskEvaluationService : IMaintenanceTaskEvaluationService
    {
        private readonly AppDbContext _context;

        public MaintenanceTaskEvaluationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<double> GetConsumedValueAsync(MaintenanceTask maintenanceTask)
        {
            if (maintenanceTask.TriggerType == MaintenanceTaskTriggerType.Time)
            {
                return Math.Max(0, (DateTime.UtcNow - maintenanceTask.StartDate).TotalDays);
            }

            return maintenanceTask.ParentType switch
            {
                MaintenanceTaskParentType.Bike => await _context.Rides
                    .Where(r => r.UserId == maintenanceTask.UserId &&
                                r.IsActive &&
                                r.BikeId == maintenanceTask.ParentId &&
                                r.StartDateLocal >= maintenanceTask.StartDate)
                    .SumAsync(r => (double?)r.Distance) ?? 0.0,

                MaintenanceTaskParentType.Part => await _context.PartUsageHistories
                    .Include(h => h.BikePart)
                    .Where(h => h.BikePart.UserId == maintenanceTask.UserId &&
                                h.BikePartId == maintenanceTask.ParentId &&
                                (
                                    (h.IsShadow && h.MaintenanceTaskId == maintenanceTask.Id) ||
                                    (!h.IsShadow && h.StartDate >= maintenanceTask.StartDate)
                                ))
                    .SumAsync(h => (double?)h.Distance) ?? 0.0,

                MaintenanceTaskParentType.ChainCycle => await GetChainCycleConsumedDistanceAsync(maintenanceTask),
                _ => 0.0
            };
        }

        public async Task<bool> NeedsAttentionAsync(MaintenanceTask maintenanceTask)
        {
            var consumed = await GetConsumedValueAsync(maintenanceTask);
            return consumed >= maintenanceTask.TriggerValue;
        }

        private async Task<double> GetChainCycleConsumedDistanceAsync(MaintenanceTask maintenanceTask)
        {
            var cycle = await _context.ChainCycles.FirstOrDefaultAsync(c => c.Id == maintenanceTask.ParentId);
            if (cycle == null)
            {
                return 0.0;
            }

            var chainIds = cycle.Chains.Where(c => c.HasValue).Select(c => c!.Value).ToList();
            if (chainIds.Count == 0)
            {
                return 0.0;
            }

            return await _context.PartUsageHistories
                .Include(h => h.BikePart)
                .Where(h => h.BikePart.UserId == maintenanceTask.UserId &&
                            chainIds.Contains(h.BikePartId) &&
                            h.StartDate >= maintenanceTask.StartDate &&
                            !h.IsShadow)
                .SumAsync(h => (double?)h.Distance) ?? 0.0;
        }
    }
}
