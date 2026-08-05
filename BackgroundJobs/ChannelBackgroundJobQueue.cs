using System.Threading.Channels;

namespace BikePartsTracker.BackgroundJobs
{
    public sealed class ChannelBackgroundJobQueue : IBackgroundJobQueue
    {
        private readonly Channel<BackgroundJob> _channel =
            Channel.CreateUnbounded<BackgroundJob>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });

        public ValueTask EnqueueAsync(BackgroundJob job, CancellationToken cancellationToken = default) =>
            _channel.Writer.WriteAsync(job, cancellationToken);

        public bool TryDequeue(out BackgroundJob? job) =>
            _channel.Reader.TryRead(out job);

        public async IAsyncEnumerable<BackgroundJob> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return job;
            }
        }
    }
}
