using BikePartsTracker.Data;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IWorkEvaluationService
    {
        Task<double> GetConsumedValueAsync(Work work);
        Task<bool> NeedsAttentionAsync(Work work);
    }

    public class WorkEvaluationService : IWorkEvaluationService
    {
        private readonly AppDbContext _context;

        public WorkEvaluationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<double> GetConsumedValueAsync(Work work)
        {
            if (work.TriggerType == WorkTriggerType.Time)
            {
                return Math.Max(0, (DateTime.UtcNow - work.StartDate).TotalDays);
            }

            return work.ParentType switch
            {
                WorkParentType.Bike => await _context.Rides
                    .Where(r => r.UserId == work.UserId &&
                                r.IsActive &&
                                r.BikeId == work.ParentId &&
                                r.StartDateLocal >= work.StartDate)
                    .SumAsync(r => (double?)r.UserDistance) ?? 0.0,

                WorkParentType.Part => await _context.PartUsageHistories
                    .Include(h => h.BikePart)
                    .Where(h => h.BikePart.UserId == work.UserId &&
                                h.BikePartId == work.ParentId &&
                                (
                                    (h.IsShadow && h.WorkId == work.Id) ||
                                    (!h.IsShadow && h.StartDate >= work.StartDate)
                                ))
                    .SumAsync(h => (double?)h.Distance) ?? 0.0,

                WorkParentType.ChainCycle => await GetChainCycleConsumedDistanceAsync(work),
                _ => 0.0
            };
        }

        public async Task<bool> NeedsAttentionAsync(Work work)
        {
            var consumed = await GetConsumedValueAsync(work);
            return consumed >= work.TriggerValue;
        }

        private async Task<double> GetChainCycleConsumedDistanceAsync(Work work)
        {
            var cycle = await _context.ChainCycles.FirstOrDefaultAsync(c => c.Id == work.ParentId);
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
                .Where(h => h.BikePart.UserId == work.UserId &&
                            chainIds.Contains(h.BikePartId) &&
                            h.StartDate >= work.StartDate &&
                            !h.IsShadow)
                .SumAsync(h => (double?)h.Distance) ?? 0.0;
        }
    }
}
