using BikePartsTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.Infrastructure;

public static class DatabaseReset
{
    private static readonly string[] Tables =
    [
        "\"StravaAthletes\"",
        "\"ExternalServiceIntegrations\"",
        "\"PartUsageHistories\"",
        "\"MaintenanceTasks\"",
        "\"Rides\"",
        "\"ChainCycles\"",
        "\"BikeParts\"",
        "\"Bikes\"",
        "\"UserSettings\"",
        "\"Users\""
    ];

    public static async Task TruncateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sql = $"TRUNCATE TABLE {string.Join(", ", Tables)} RESTART IDENTITY CASCADE;";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
