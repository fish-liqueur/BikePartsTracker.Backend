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

            var affectedWorkIds = new HashSet<Guid>();

            if (partIdSet.Count > 0)
            {
                var partWorks = await _context.Works
                    .Where(w => w.UserId == userId &&
                                w.ParentType == WorkParentType.Part &&
                                partIdSet.Contains(w.ParentId))
                    .Select(w => w.Id)
                    .ToListAsync();
                affectedWorkIds.UnionWith(partWorks);
            }

            if (bikeIdSet.Count > 0)
            {
                var bikeWorks = await _context.Works
                    .Where(w => w.UserId == userId &&
                                w.ParentType == WorkParentType.Bike &&
                                bikeIdSet.Contains(w.ParentId))
                    .Select(w => w.Id)
                    .ToListAsync();
                affectedWorkIds.UnionWith(bikeWorks);
            }

            if (partIdSet.Count > 0)
            {
                var cycleWorks = await _context.Works
                    .Where(w => w.UserId == userId && w.ParentType == WorkParentType.ChainCycle)
                    .Join(_context.ChainCycles,
                        w => w.ParentId,
                        c => c.Id,
                        (w, c) => new { Work = w, Cycle = c })
                    .ToListAsync();

                foreach (var entry in cycleWorks)
                {
                    var chains = entry.Cycle.Chains;
                    if (chains.Any(chainId => chainId.HasValue && partIdSet.Contains(chainId.Value)))
                    {
                        affectedWorkIds.Add(entry.Work.Id);
                    }
                }
            }

            return new RideMutationResultDto
            {
                AffectedRideIds = rideIdList,
                AffectedPartIds = partIdSet.ToList(),
                AffectedBikeIds = bikeIdSet.ToList(),
                AffectedWorkIds = affectedWorkIds.ToList()
            };
        }
    }
}
