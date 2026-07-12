using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IRideMutationResolver
    {
        Task<RideMutationResultDto> BuildAsync(
            Guid userId,
            IEnumerable<Guid> rideIds,
            IEnumerable<Guid> partIds,
            IEnumerable<Guid?> bikeIds);
    }

    public class RideMutationResolver : IRideMutationResolver
    {
        private readonly AppDbContext _context;

        public RideMutationResolver(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RideMutationResultDto> BuildAsync(
            Guid userId,
            IEnumerable<Guid> rideIds,
            IEnumerable<Guid> partIds,
            IEnumerable<Guid?> bikeIds)
        {
            var partIdSet = partIds.Distinct().ToHashSet();
            var bikeIdSet = bikeIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToHashSet();
            var rideIdList = rideIds.Distinct().ToList();

            var affectedMaintenanceTaskIds = new HashSet<Guid>();

            if (partIdSet.Count > 0)
            {
                var partMaintenanceTasks = await _context.MaintenanceTasks
                    .Where(w => w.UserId == userId &&
                                w.ParentType == MaintenanceTaskParentType.Part &&
                                partIdSet.Contains(w.ParentId))
                    .Select(w => w.Id)
                    .ToListAsync();
                affectedMaintenanceTaskIds.UnionWith(partMaintenanceTasks);
            }

            if (bikeIdSet.Count > 0)
            {
                var bikeMaintenanceTasks = await _context.MaintenanceTasks
                    .Where(w => w.UserId == userId &&
                                w.ParentType == MaintenanceTaskParentType.Bike &&
                                bikeIdSet.Contains(w.ParentId))
                    .Select(w => w.Id)
                    .ToListAsync();
                affectedMaintenanceTaskIds.UnionWith(bikeMaintenanceTasks);
            }

            if (partIdSet.Count > 0)
            {
                var cycleMaintenanceTasks = await _context.MaintenanceTasks
                    .Where(w => w.UserId == userId && w.ParentType == MaintenanceTaskParentType.ChainCycle)
                    .Join(_context.ChainCycles,
                        w => w.ParentId,
                        c => c.Id,
                        (w, c) => new { MaintenanceTask = w, Cycle = c })
                    .ToListAsync();

                foreach (var entry in cycleMaintenanceTasks)
                {
                    var chains = entry.Cycle.Chains;
                    if (chains.Any(chainId => chainId.HasValue && partIdSet.Contains(chainId.Value)))
                    {
                        affectedMaintenanceTaskIds.Add(entry.MaintenanceTask.Id);
                    }
                }
            }

            return new RideMutationResultDto
            {
                AffectedRideIds = rideIdList,
                AffectedPartIds = partIdSet.ToList(),
                AffectedBikeIds = bikeIdSet.ToList(),
                AffectedMaintenanceTaskIds = affectedMaintenanceTaskIds.ToList()
            };
        }
    }
}
