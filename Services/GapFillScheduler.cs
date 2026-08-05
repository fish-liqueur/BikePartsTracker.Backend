using BikePartsTracker.BackgroundJobs;

namespace BikePartsTracker.Services
{
    public interface IGapFillScheduler
    {
        /// <summary>
        /// Enqueues gap-fill jobs when a real usage period opens with a past StartDate.
        /// Does not block on Strava I/O.
        /// </summary>
        Task ScheduleIfNeededAsync(Guid userId, DateTime periodStartDate, CancellationToken cancellationToken = default);
    }

    public class GapFillScheduler : IGapFillScheduler
    {
        private readonly IStravaIntegrationService _stravaIntegrationService;
        private readonly IBackgroundJobQueue _jobQueue;
        private readonly ILogger<GapFillScheduler> _logger;

        public GapFillScheduler(
            IStravaIntegrationService stravaIntegrationService,
            IBackgroundJobQueue jobQueue,
            ILogger<GapFillScheduler> logger)
        {
            _stravaIntegrationService = stravaIntegrationService;
            _jobQueue = jobQueue;
            _logger = logger;
        }

        public async Task ScheduleIfNeededAsync(
            Guid userId,
            DateTime periodStartDate,
            CancellationToken cancellationToken = default)
        {
            var utcToday = DateTime.UtcNow.Date;
            if (periodStartDate.Date >= utcToday)
            {
                return;
            }

            var integration = await _stravaIntegrationService.GetUserStravaIntegrationAsync(userId);
            if (integration == null)
            {
                return;
            }

            var gaps = GapFillCalculator.ComputeMissingRanges(
                periodStartDate,
                utcToday,
                integration.AutoImportCoveredFrom,
                integration.AutoImportCoveredTo);

            foreach (var gap in gaps)
            {
                _logger.LogInformation(
                    "Enqueueing gap-fill for user {UserId} from {From:yyyy-MM-dd} to {To:yyyy-MM-dd}",
                    userId, gap.From, gap.To);

                await _jobQueue.EnqueueAsync(new BackgroundJob
                {
                    Kind = BackgroundJobKind.GapFillAutoImport,
                    UserId = userId,
                    RangeFrom = DateTime.SpecifyKind(gap.From, DateTimeKind.Utc),
                    RangeTo = DateTime.SpecifyKind(gap.To, DateTimeKind.Utc)
                }, cancellationToken);
            }
        }
    }
}
