using BikePartsTracker.BackgroundJobs;
using BikePartsTracker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.Infrastructure;

public static class BackgroundJobTestHelper
{
    /// <summary>
    /// Drains the in-process channel by running each job through IBackgroundJobHandler.
    /// </summary>
    public static async Task DrainAsync(IServiceProvider services, int maxJobs = 50)
    {
        var queue = services.GetRequiredService<IBackgroundJobQueue>();
        var processed = 0;
        while (processed < maxJobs && queue.TryDequeue(out var job) && job != null)
        {
            await using var scope = services.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IBackgroundJobHandler>();
            await handler.HandleAsync(job);
            processed++;
        }
    }
}
