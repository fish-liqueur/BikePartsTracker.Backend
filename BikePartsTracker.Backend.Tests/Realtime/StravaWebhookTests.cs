using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.Realtime;

public class StravaWebhookTests : IntegrationTestBase
{
    private const long AthleteIdA = 10001;
    private const long ActivityId = 90001;

    public StravaWebhookTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    private async Task SeedStravaIntegrationAsync(Guid userId, long athleteId)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Users.AnyAsync(u => u.Id == userId))
        {
            db.Users.Add(new User
            {
                Id = userId,
                Name = "User",
                Email = $"{userId}@example.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow
            });
        }

        db.ExternalServiceIntegrations.Add(new ExternalServiceIntegration
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = null!,
            ServiceType = ExternalServiceType.Strava,
            ServiceUserId = athleteId.ToString(),
            AccessToken = "access",
            RefreshToken = "refresh",
            TokenExpiry = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // W-01
    [Fact]
    public async Task Hub_validation_with_correct_token_echoes_challenge()
    {
        var response = await Client.GetAsync(
            $"/api/strava/webhook?hub.mode=subscribe&hub.challenge=abc123&hub.verify_token={BikePartsTrackerWebApplicationFactory.WebhookVerifyToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.Equal("abc123", body["hub.challenge"]);
    }

    // W-02
    [Fact]
    public async Task Hub_validation_with_wrong_token_rejects()
    {
        var response = await Client.GetAsync(
            "/api/strava/webhook?hub.mode=subscribe&hub.challenge=abc123&hub.verify_token=wrong");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // W-03 + W-04
    [Fact]
    public async Task Post_webhook_anonymous_acks_immediately_without_strava_call()
    {
        ClearAuth();
        Fixture.Factory.FakeStrava.ActivitiesById[ActivityId] = new StravaActivityDto
        {
            Id = ActivityId,
            Name = "Ride",
            Distance = 1000,
            Type = "Ride",
            StartDateLocal = DateTime.UtcNow
        };

        var response = await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "create",
            ObjectId = ActivityId,
            OwnerId = AthleteIdA,
            EventTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, Fixture.Factory.FakeStrava.GetActivityCallCount);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Rides.CountAsync());
    }

    // W-05
    [Fact]
    public async Task Unknown_owner_acks_and_noops()
    {
        ClearAuth();
        var response = await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "create",
            ObjectId = ActivityId,
            OwnerId = 99999,
            EventTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        Assert.Equal(0, Fixture.Factory.FakeStrava.GetActivityCallCount);
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Rides.CountAsync());
    }

    // W-06 + I-01
    [Fact]
    public async Task Activity_create_upserts_ride_idempotently()
    {
        await SeedTwoUsersWithBikesAsync();
        await SeedStravaIntegrationAsync(UserAId, AthleteIdA);

        var partId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var periodStart = DateTime.UtcNow.Date.AddDays(-2);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == UserAId);
            var bike = await db.Bikes.FirstAsync(b => b.Id == BikeAId);
            bike.StravaBikeId = "b1001";
            db.BikeParts.Add(new BikePart
            {
                Id = partId,
                UserId = UserAId,
                User = user,
                Name = "Chain",
                PartType = PartType.Chain,
                IsActive = true,
                BikeId = BikeAId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.PartUsageHistories.Add(new PartUsageHistory
            {
                Id = Guid.NewGuid(),
                BikePartId = partId,
                BikePart = null!,
                BikeId = BikeAId,
                StartDate = periodStart,
                EndDate = null,
                Distance = 0,
                IsShadow = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var start = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(10), DateTimeKind.Unspecified);
        Fixture.Factory.FakeStrava.ActivitiesById[ActivityId] = new StravaActivityDto
        {
            Id = ActivityId,
            Name = "Morning ride",
            Distance = 25000,
            Type = "Ride",
            GearId = "b1001",
            StartDateLocal = start
        };

        ClearAuth();
        for (var i = 0; i < 2; i++)
        {
            var response = await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
            {
                ObjectType = "activity",
                AspectType = "create",
                ObjectId = ActivityId,
                OwnerId = AthleteIdA,
                EventTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);
        }

        await using var verify = Fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var rides = await verifyDb.Rides.Where(r => r.UserId == UserAId).ToListAsync();
        Assert.Single(rides);
        Assert.Equal(ActivityId, rides[0].StravaActivityId);
        Assert.Equal("Morning ride", rides[0].Name);
        Assert.Equal(BikeAId, rides[0].BikeId);

        var period = await verifyDb.PartUsageHistories.SingleAsync(h => h.BikePartId == partId && !h.IsShadow);
        Assert.Equal(25000, period.Distance);

        var integration = await verifyDb.ExternalServiceIntegrations.SingleAsync(i => i.UserId == UserAId);
        Assert.Equal(start.Date, integration.AutoImportCoveredFrom?.Date);
        Assert.Equal(start.Date, integration.AutoImportCoveredTo?.Date);
    }

    // I-02
    [Fact]
    public async Task Activity_gear_maps_to_strava_bike_id()
    {
        await SeedTwoUsersWithBikesAsync();
        await SeedStravaIntegrationAsync(UserAId, AthleteIdA);

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bike = await db.Bikes.FirstAsync(b => b.Id == BikeAId);
            bike.StravaBikeId = "b1234";
            await db.SaveChangesAsync();
        }

        Fixture.Factory.FakeStrava.ActivitiesById[ActivityId] = new StravaActivityDto
        {
            Id = ActivityId,
            Name = "Gear ride",
            Distance = 10000,
            Type = "Ride",
            GearId = "b1234",
            StartDateLocal = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified)
        };

        ClearAuth();
        await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "create",
            ObjectId = ActivityId,
            OwnerId = AthleteIdA
        });
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        await using var verify = Fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var ride = await verifyDb.Rides.SingleAsync(r => r.StravaActivityId == ActivityId);
        Assert.Equal(BikeAId, ride.BikeId);
    }

    // I-03
    [Fact]
    public async Task Activity_update_preserves_manual_distance_correction()
    {
        await SeedTwoUsersWithBikesAsync();
        await SeedStravaIntegrationAsync(UserAId, AthleteIdA);

        var start = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Rides.Add(new Ride
            {
                Id = Guid.NewGuid(),
                UserId = UserAId,
                User = null!,
                StravaActivityId = ActivityId,
                Name = "Corrected",
                Type = "Ride",
                RecordedDistance = 10000,
                Distance = 8000, // rider correction ratio 0.8
                StartDateLocal = start,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        Fixture.Factory.FakeStrava.ActivitiesById[ActivityId] = new StravaActivityDto
        {
            Id = ActivityId,
            Name = "Corrected",
            Distance = 20000, // Strava updated distance
            Type = "Ride",
            StartDateLocal = start
        };

        ClearAuth();
        await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "update",
            ObjectId = ActivityId,
            OwnerId = AthleteIdA
        });
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        await using var verify = Fixture.Factory.Services.CreateAsyncScope();
        var ride = await verify.ServiceProvider.GetRequiredService<AppDbContext>()
            .Rides.SingleAsync(r => r.StravaActivityId == ActivityId);
        Assert.Equal(20000, ride.RecordedDistance);
        Assert.Equal(16000, ride.Distance); // 20000 * 0.8
    }

    // I-04
    [Fact]
    public async Task Activity_delete_removes_ride()
    {
        await SeedTwoUsersWithBikesAsync();
        await SeedStravaIntegrationAsync(UserAId, AthleteIdA);

        var partId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var rideId = Guid.NewGuid();
        var start = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == UserAId);
            db.BikeParts.Add(new BikePart
            {
                Id = partId,
                UserId = UserAId,
                User = user,
                Name = "Chain",
                PartType = PartType.Chain,
                IsActive = true,
                BikeId = BikeAId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Rides.Add(new Ride
            {
                Id = rideId,
                UserId = UserAId,
                User = user,
                BikeId = BikeAId,
                StravaActivityId = ActivityId,
                Name = "Gone",
                Type = "Ride",
                RecordedDistance = 1000,
                Distance = 1000,
                StartDateLocal = start,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.PartUsageHistories.Add(new PartUsageHistory
            {
                Id = Guid.NewGuid(),
                BikePartId = partId,
                BikePart = null!,
                BikeId = BikeAId,
                StartDate = start.AddDays(-1),
                EndDate = null,
                Distance = 1000,
                IsShadow = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        ClearAuth();
        await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "delete",
            ObjectId = ActivityId,
            OwnerId = AthleteIdA
        });
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        await using var verify = Fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await verifyDb.Rides.AnyAsync(r => r.StravaActivityId == ActivityId));
        var period = await verifyDb.PartUsageHistories.SingleAsync(h => h.BikePartId == partId && !h.IsShadow);
        Assert.Equal(0, period.Distance);
    }

    // I-05
    [Fact]
    public async Task Foreign_owner_id_does_not_mutate_other_users_garage()
    {
        await SeedTwoUsersWithBikesAsync();
        await SeedStravaIntegrationAsync(UserAId, AthleteIdA);
        await SeedStravaIntegrationAsync(UserBId, 20002);

        Fixture.Factory.FakeStrava.ActivitiesById[ActivityId] = new StravaActivityDto
        {
            Id = ActivityId,
            Name = "B's ride",
            Distance = 5000,
            Type = "Ride",
            StartDateLocal = DateTime.UtcNow
        };

        // Event claims owner is B's athlete — must not write under A
        ClearAuth();
        await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "create",
            ObjectId = ActivityId,
            OwnerId = 20002
        });
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Rides.CountAsync(r => r.UserId == UserAId));
        Assert.Equal(1, await db.Rides.CountAsync(r => r.UserId == UserBId));
    }

    // I-06
    [Fact]
    public async Task Activity_create_trips_distance_task_needs_attention()
    {
        await SeedTwoUsersWithBikesAsync();
        await SeedStravaIntegrationAsync(UserAId, AthleteIdA);

        var taskId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var taskStart = DateTime.UtcNow.Date.AddDays(-7);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == UserAId);
            var bike = await db.Bikes.FirstAsync(b => b.Id == BikeAId);
            bike.StravaBikeId = "b1001";
            db.MaintenanceTasks.Add(new MaintenanceTask
            {
                Id = taskId,
                UserId = UserAId,
                User = user,
                Name = "Service",
                StartDate = taskStart,
                Type = MaintenanceTaskType.OneTime,
                TriggerType = MaintenanceTaskTriggerType.Distance,
                ParentType = MaintenanceTaskParentType.Bike,
                ParentId = BikeAId,
                TriggerValue = 5000,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        Fixture.Factory.FakeStrava.ActivitiesById[ActivityId] = new StravaActivityDto
        {
            Id = ActivityId,
            Name = "Long ride",
            Distance = 10000,
            Type = "Ride",
            GearId = "b1001",
            StartDateLocal = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified)
        };

        ClearAuth();
        await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "create",
            ObjectId = ActivityId,
            OwnerId = AthleteIdA
        });
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        AuthenticateAsUserA();
        var response = await Client.GetAsync($"/api/maintenance-tasks?parentType=Bike&parentId={BikeAId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await ReadJsonAsync<List<MaintenanceTaskDto>>(response);
        Assert.NotNull(tasks);
        var task = Assert.Single(tasks!);
        Assert.Equal(taskId, task.Id);
        Assert.True(task.NeedsAttention);
        Assert.True(task.ConsumedValue >= task.TriggerValue);
    }

    // I-07
    [Fact]
    public async Task Athlete_deauthorize_removes_integration()
    {
        await SeedTwoUsersWithBikesAsync();
        await SeedStravaIntegrationAsync(UserAId, AthleteIdA);

        ClearAuth();
        await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "athlete",
            AspectType = "update",
            ObjectId = AthleteIdA,
            OwnerId = AthleteIdA,
            Updates = new Dictionary<string, string> { ["authorized"] = "false" }
        });
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.ExternalServiceIntegrations.AnyAsync(i => i.UserId == UserAId));
        }

        // Follow-up activity for that athlete ACK/drops with no garage mutation
        Fixture.Factory.FakeStrava.Reset();
        Fixture.Factory.FakeStrava.ActivitiesById[ActivityId] = new StravaActivityDto
        {
            Id = ActivityId,
            Name = "After revoke",
            Distance = 1000,
            Type = "Ride",
            StartDateLocal = DateTime.UtcNow
        };
        await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "create",
            ObjectId = ActivityId,
            OwnerId = AthleteIdA
        });
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        Assert.Equal(0, Fixture.Factory.FakeStrava.GetActivityCallCount);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await db.Rides.CountAsync(r => r.UserId == UserAId));
        }
    }

    // I-08
    [Fact]
    public async Task No_integration_noops()
    {
        await SeedTwoUsersWithBikesAsync();
        // no integration seeded

        ClearAuth();
        await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "create",
            ObjectId = ActivityId,
            OwnerId = AthleteIdA
        });
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        Assert.Equal(0, Fixture.Factory.FakeStrava.GetActivityCallCount);
    }
}
