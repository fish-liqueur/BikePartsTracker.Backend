using BikePartsTracker.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BikePartsTracker.Hubs
{
    [Authorize]
    public class UpdatesHub : Hub
    {
        public const string HubPath = "/hubs/updates";
        public const string EntitiesAffectedMethod = "entitiesAffected";

        public static string UserGroup(Guid userId) => $"user:{userId}";

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
            }

            await base.OnConnectedAsync();
        }

        private Guid? GetUserId()
        {
            var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public interface IRealtimeNotifier
    {
        Task NotifyEntitiesAffectedAsync(Guid userId, RideMutationResultDto affected, CancellationToken cancellationToken = default);
    }

    public class SignalRRealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<UpdatesHub> _hub;

        public SignalRRealtimeNotifier(IHubContext<UpdatesHub> hub)
        {
            _hub = hub;
        }

        public Task NotifyEntitiesAffectedAsync(
            Guid userId,
            RideMutationResultDto affected,
            CancellationToken cancellationToken = default)
        {
            if (affected.AffectedRideIds.Count == 0 &&
                affected.AffectedPartIds.Count == 0 &&
                affected.AffectedBikeIds.Count == 0 &&
                affected.AffectedMaintenanceTaskIds.Count == 0)
            {
                return Task.CompletedTask;
            }
            return _hub.Clients
                .Group(UpdatesHub.UserGroup(userId))
                .SendAsync(UpdatesHub.EntitiesAffectedMethod, affected, cancellationToken);
        }
    }
}
