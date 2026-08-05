namespace BikePartsTracker.BackgroundJobs
{
    public interface IBackgroundJobQueue
    {
        ValueTask EnqueueAsync(BackgroundJob job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tries to dequeue without waiting. Used by tests to drain the in-process channel.
        /// </summary>
        bool TryDequeue(out BackgroundJob? job);

        IAsyncEnumerable<BackgroundJob> ReadAllAsync(CancellationToken cancellationToken);
    }
}
