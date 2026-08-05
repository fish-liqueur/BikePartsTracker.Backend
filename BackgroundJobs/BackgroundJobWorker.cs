using BikePartsTracker.Services;

namespace BikePartsTracker.BackgroundJobs
{
    /// <summary>
    /// In-process worker: one IServiceScope per job so scoped DbContext/services stay correct.
    /// Not registered in the Testing environment — tests drain the queue explicitly.
    /// </summary>
    public sealed class BackgroundJobWorker : BackgroundService
    {
        private readonly IBackgroundJobQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundJobWorker> _logger;

        public BackgroundJobWorker(
            IBackgroundJobQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundJobWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IBackgroundJobHandler>();
                    await handler.HandleAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error processing background job {Kind}", job.Kind);
                }
            }
        }
    }
}
