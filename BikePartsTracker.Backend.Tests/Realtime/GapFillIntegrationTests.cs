using System.Net;
using System.Net.Http.Json;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.BackgroundJobs;
using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.Realtime;

public class GapFillIntegrationTests : IntegrationTestBase
{
    private const long AthleteIdA = 10001;
    private static readonly Guid PartAId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public GapFillIntegrationTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    private async Task SeedPartAndStravaAsync()
    {
        await SeedTwoUsersWithBikesAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == UserAId);
        db.BikeParts.Add(new BikePart
        {
            Id = PartAId,
            UserId = UserAId,
            User = user,
            Name = "Chain",
            PartType = PartType.Chain,
            IsActive = true,
            BikeId = BikeAId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.ExternalServiceIntegrations.Add(new ExternalServiceIntegration
        {
            Id = Guid.NewGuid(),
            UserId = UserAId,
            User = user,
            ServiceType = ExternalServiceType.Strava,
            ServiceUserId = AthleteIdA.ToString(),
            AccessToken = "access",
            RefreshToken = "refresh",
            TokenExpiry = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // G-07
    [Fact]
    public async Task Opening_past_usage_period_enqueues_gap_fill_without_blocking_on_strava()
    {
        await SeedPartAndStravaAsync();
        AuthenticateAsUserA();

        var start = DateTime.UtcNow.Date.AddDays(-10);
        var response = await Client.PostAsJsonAsync("/api/usageperiods", new CreateUsagePeriodDto
        {
            BikePartId = PartAId,
            BikeId = BikeAId,
            StartDate = start
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Response returned without draining — Strava not called yet
        Assert.Equal(0, Fixture.Factory.FakeStrava.GetActivitiesCallCount);

        var queue = Fixture.Factory.Services.GetRequiredService<IBackgroundJobQueue>();
        Assert.True(queue.TryDequeue(out var job));
        Assert.NotNull(job);
        Assert.Equal(BackgroundJobKind.GapFillAutoImport, job!.Kind);
        Assert.Equal(UserAId, job.UserId);
    }

    // G-08
    [Fact]
    public async Task Strava_connect_alone_does_not_enqueue_gap_fill()
    {
        await SeedTwoUsersWithBikesAsync();
        // Simulate only having an integration (no period open)
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ExternalServiceIntegrations.Add(new ExternalServiceIntegration
        {
            Id = Guid.NewGuid(),
            UserId = UserAId,
            User = null!,
            ServiceType = ExternalServiceType.Strava,
            ServiceUserId = AthleteIdA.ToString(),
            AccessToken = "access",
            RefreshToken = "refresh",
            TokenExpiry = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var queue = Fixture.Factory.Services.GetRequiredService<IBackgroundJobQueue>();
        Assert.False(queue.TryDequeue(out _));
    }

    // G-09
    [Fact]
    public async Task Part_edit_without_opening_past_period_does_not_enqueue_gap_fill()
    {
        await SeedPartAndStravaAsync();
        AuthenticateAsUserA();

        var response = await Client.PutAsJsonAsync($"/api/parts/{PartAId}", new UpdatePartDto
        {
            Name = "Chain renamed"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var queue = Fixture.Factory.Services.GetRequiredService<IBackgroundJobQueue>();
        Assert.False(queue.TryDequeue(out _));
    }

    // Gap-fill on usage-period StartDate edit (review finding #3)
    [Fact]
    public async Task Updating_usage_period_start_into_past_enqueues_gap_fill()
    {
        await SeedPartAndStravaAsync();
        AuthenticateAsUserA();

        var create = await Client.PostAsJsonAsync("/api/usageperiods", new CreateUsagePeriodDto
        {
            BikePartId = PartAId,
            BikeId = BikeAId,
            StartDate = DateTime.UtcNow.Date
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        // Drain create-day gap-fill (today-only window may still enqueue)
        while (Fixture.Factory.Services.GetRequiredService<IBackgroundJobQueue>().TryDequeue(out _))
        {
        }

        var created = await create.Content.ReadFromJsonAsync<UsagePeriodDto>();
        Assert.NotNull(created);

        var update = await Client.PutAsJsonAsync($"/api/usageperiods/{created!.Id}", new UpdateUsagePeriodDto
        {
            StartDate = DateTime.UtcNow.Date.AddDays(-12)
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var queue = Fixture.Factory.Services.GetRequiredService<IBackgroundJobQueue>();
        Assert.True(queue.TryDequeue(out var job));
        Assert.Equal(BackgroundJobKind.GapFillAutoImport, job!.Kind);
    }

    // G-10
    [Fact]
    public async Task Gap_fill_success_updates_watermark_not_on_rider_dtos()
    {
        await SeedPartAndStravaAsync();
        AuthenticateAsUserA();

        var start = DateTime.UtcNow.Date.AddDays(-5);
        Fixture.Factory.FakeStrava.Activities.Add(new StravaActivityDto
        {
            Id = 55,
            Name = "Gap ride",
            Distance = 1000,
            Type = "Ride",
            StartDateLocal = start.AddDays(1)
        });

        var response = await Client.PostAsJsonAsync("/api/usageperiods", new CreateUsagePeriodDto
        {
            BikePartId = PartAId,
            BikeId = BikeAId,
            StartDate = start
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.DoesNotContain("AutoImportCovered", body, StringComparison.OrdinalIgnoreCase);

        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var integration = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ExternalServiceIntegrations.SingleAsync(i => i.UserId == UserAId);
        Assert.NotNull(integration.AutoImportCoveredFrom);
        Assert.NotNull(integration.AutoImportCoveredTo);
    }
}
