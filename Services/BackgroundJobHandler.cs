using BikePartsTracker.BackgroundJobs;
using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Hubs;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IBackgroundJobHandler
    {
        Task HandleAsync(BackgroundJob job, CancellationToken cancellationToken = default);
    }

    public class BackgroundJobHandler : IBackgroundJobHandler
    {
        private const int MaxAttempts = 3;

        private readonly AppDbContext _context;
        private readonly IStravaService _stravaService;
        private readonly IStravaIntegrationService _stravaIntegrationService;
        private readonly IRideImportService _rideImportService;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly IBackgroundJobQueue _jobQueue;
        private readonly ILogger<BackgroundJobHandler> _logger;

        public BackgroundJobHandler(
            AppDbContext context,
            IStravaService stravaService,
            IStravaIntegrationService stravaIntegrationService,
            IRideImportService rideImportService,
            IRealtimeNotifier realtimeNotifier,
            IBackgroundJobQueue jobQueue,
            ILogger<BackgroundJobHandler> logger)
        {
            _context = context;
            _stravaService = stravaService;
            _stravaIntegrationService = stravaIntegrationService;
            _rideImportService = rideImportService;
            _realtimeNotifier = realtimeNotifier;
            _jobQueue = jobQueue;
            _logger = logger;
        }

        public async Task HandleAsync(BackgroundJob job, CancellationToken cancellationToken = default)
        {
            try
            {
                switch (job.Kind)
                {
                    case BackgroundJobKind.ProcessStravaWebhook:
                        await HandleWebhookAsync(job, cancellationToken);
                        break;
                    case BackgroundJobKind.GapFillAutoImport:
                        await HandleGapFillAsync(job, cancellationToken);
                        break;
                    default:
                        _logger.LogWarning("Unknown background job kind {Kind}", job.Kind);
                        break;
                }
            }
            catch (Exception ex) when (job.Attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "Background job {Kind} attempt {Attempt} failed; re-enqueueing", job.Kind, job.Attempt);
                await _jobQueue.EnqueueAsync(new BackgroundJob
                {
                    Kind = job.Kind,
                    Attempt = job.Attempt + 1,
                    OwnerId = job.OwnerId,
                    ObjectType = job.ObjectType,
                    AspectType = job.AspectType,
                    ObjectId = job.ObjectId,
                    Updates = job.Updates,
                    UserId = job.UserId,
                    RangeFrom = job.RangeFrom,
                    RangeTo = job.RangeTo
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job {Kind} failed after {Attempt} attempts", job.Kind, job.Attempt);
            }
        }

        private async Task HandleWebhookAsync(BackgroundJob job, CancellationToken cancellationToken)
        {
            var ownerId = job.OwnerId?.ToString();
            if (string.IsNullOrEmpty(ownerId))
            {
                return;
            }

            var integration = await _context.ExternalServiceIntegrations
                .FirstOrDefaultAsync(
                    i => i.ServiceType == ExternalServiceType.Strava && i.ServiceUserId == ownerId,
                    cancellationToken);

            if (integration == null)
            {
                _logger.LogInformation("Strava webhook for unknown owner_id {OwnerId}; dropping", ownerId);
                return;
            }

            var objectType = job.ObjectType?.ToLowerInvariant();
            var aspectType = job.AspectType?.ToLowerInvariant();

            if (objectType == "athlete" && aspectType == "update")
            {
                if (job.Updates != null &&
                    job.Updates.TryGetValue("authorized", out var authorized) &&
                    string.Equals(authorized, "false", StringComparison.OrdinalIgnoreCase))
                {
                    _context.ExternalServiceIntegrations.Remove(integration);
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Removed Strava integration for user {UserId} after deauthorize webhook", integration.UserId);
                }

                return;
            }

            if (objectType != "activity" || !job.ObjectId.HasValue)
            {
                return;
            }

            RideMutationResultDto? affected = null;

            if (aspectType is "create" or "update")
            {
                var result = await _rideImportService.UpsertStravaActivityAsync(integration.UserId, job.ObjectId.Value);
                affected = result?.Affected;

                // Contiguous watermark: seed/expand for this activity day when possible (ADR-0001 #6).
                if (result?.ActivityDate is { } activityDay &&
                    GapFillCalculator.TryExpandWatermarkForDay(
                        integration.AutoImportCoveredFrom,
                        integration.AutoImportCoveredTo,
                        activityDay,
                        out var newFrom,
                        out var newTo))
                {
                    integration.AutoImportCoveredFrom = DateTime.SpecifyKind(newFrom, DateTimeKind.Utc);
                    integration.AutoImportCoveredTo = DateTime.SpecifyKind(newTo, DateTimeKind.Utc);
                    integration.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            else if (aspectType == "delete")
            {
                affected = await _rideImportService.DeleteByStravaActivityIdAsync(integration.UserId, job.ObjectId.Value);
            }

            if (affected != null)
            {
                await _realtimeNotifier.NotifyEntitiesAffectedAsync(integration.UserId, affected, cancellationToken);
            }
        }

        private async Task HandleGapFillAsync(BackgroundJob job, CancellationToken cancellationToken)
        {
            if (!job.UserId.HasValue || !job.RangeFrom.HasValue || !job.RangeTo.HasValue)
            {
                return;
            }

            var userId = job.UserId.Value;
            var from = job.RangeFrom.Value.Date;
            // Import uses exclusive-ish unix bounds via DateTimeOffset; include the full end day
            var to = job.RangeTo.Value.Date.AddDays(1).AddTicks(-1);

            var integration = await _stravaIntegrationService.GetUserStravaIntegrationAsync(userId);
            if (integration == null)
            {
                return;
            }

            var result = await _rideImportService.ImportFromStravaAsync(userId, from, to);

            // Expand watermark after successful fetch
            var (newFrom, newTo) = GapFillCalculator.ExpandWatermark(
                integration.AutoImportCoveredFrom,
                integration.AutoImportCoveredTo,
                job.RangeFrom.Value,
                job.RangeTo.Value);

            integration.AutoImportCoveredFrom = DateTime.SpecifyKind(newFrom, DateTimeKind.Utc);
            integration.AutoImportCoveredTo = DateTime.SpecifyKind(newTo, DateTimeKind.Utc);
            integration.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await _realtimeNotifier.NotifyEntitiesAffectedAsync(userId, result.Affected, cancellationToken);
        }
    }
}
