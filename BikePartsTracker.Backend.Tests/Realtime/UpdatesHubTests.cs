using System.Net;
using System.Net.Http.Json;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Hubs;
using BikePartsTracker.Models;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.Realtime;

public class UpdatesHubTests : IntegrationTestBase
{
    private const long AthleteIdA = 10001;
    private const long ActivityId = 90001;

    public UpdatesHubTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    private string MintToken(Guid userId, string email, string name) =>
        JwtTestHelper.Mint(
            userId, email, name,
            IntegrationTestFixture.JwtKey,
            IntegrationTestFixture.JwtIssuer,
            IntegrationTestFixture.JwtAudience);

    private async Task<HubConnection> ConnectAsync(string? token)
    {
        var builder = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, UpdatesHub.HubPath), options =>
            {
                options.HttpMessageHandlerFactory = _ => Fixture.Factory.Server.CreateHandler();
                if (!string.IsNullOrEmpty(token))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
                options.Transports = HttpTransportType.LongPolling;
            });

        var connection = builder.Build();
        await connection.StartAsync();
        return connection;
    }

    // H-01
    [Fact]
    public async Task Connect_without_jwt_is_rejected()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var connection = await ConnectAsync(null);
        });
    }

    // H-02 + H-03
    [Fact]
    public async Task User_a_receives_entities_affected_user_b_does_not()
    {
        await SeedTwoUsersWithBikesAsync();
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == UserAId);
            var bike = await db.Bikes.FirstAsync(b => b.Id == BikeAId);
            bike.StravaBikeId = "b1001";
            var partId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var taskId = Guid.Parse("44444444-4444-4444-4444-444444444444");
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
                StartDate = DateTime.UtcNow.Date.AddDays(-1),
                EndDate = null,
                Distance = 0,
                IsShadow = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.MaintenanceTasks.Add(new MaintenanceTask
            {
                Id = taskId,
                UserId = UserAId,
                User = user,
                Name = "Lube",
                StartDate = DateTime.UtcNow.Date.AddDays(-7),
                Type = MaintenanceTaskType.OneTime,
                TriggerType = MaintenanceTaskTriggerType.Distance,
                ParentType = MaintenanceTaskParentType.Part,
                ParentId = partId,
                TriggerValue = 100000,
                IsActive = true,
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

        Fixture.Factory.FakeStrava.ActivitiesById[ActivityId] = new StravaActivityDto
        {
            Id = ActivityId,
            Name = "Live ride",
            Distance = 5000,
            Type = "Ride",
            GearId = "b1001",
            StartDateLocal = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified)
        };

        var tokenA = MintToken(UserAId, "a@example.com", "User A");
        var tokenB = MintToken(UserBId, "b@example.com", "User B");

        await using var connA = await ConnectAsync(tokenA);
        await using var connB = await ConnectAsync(tokenB);

        RideMutationResultDto? receivedA = null;
        RideMutationResultDto? receivedB = null;
        var tcsA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        connA.On<RideMutationResultDto>(UpdatesHub.EntitiesAffectedMethod, dto =>
        {
            receivedA = dto;
            tcsA.TrySetResult();
        });
        connB.On<RideMutationResultDto>(UpdatesHub.EntitiesAffectedMethod, dto =>
        {
            receivedB = dto;
        });

        ClearAuth();
        var post = await Client.PostAsJsonAsync("/api/strava/webhook", new StravaWebhookEventDto
        {
            ObjectType = "activity",
            AspectType = "create",
            ObjectId = ActivityId,
            OwnerId = AthleteIdA
        });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        await BackgroundJobTestHelper.DrainAsync(Fixture.Factory.Services);

        var completed = await Task.WhenAny(tcsA.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(tcsA.Task, completed);

        Assert.NotNull(receivedA);
        Assert.NotEmpty(receivedA!.AffectedRideIds);
        Assert.Contains(BikeAId, receivedA.AffectedBikeIds);
        Assert.Contains(Guid.Parse("33333333-3333-3333-3333-333333333333"), receivedA.AffectedPartIds);
        Assert.Contains(Guid.Parse("44444444-4444-4444-4444-444444444444"), receivedA.AffectedMaintenanceTaskIds);
        Assert.Null(receivedB);
    }

    // H-04
    [Fact]
    public async Task Ride_mutation_fans_out_entities_affected()
    {
        await SeedTwoUsersWithBikesAsync();
        var tokenA = MintToken(UserAId, "a@example.com", "User A");
        await using var connA = await ConnectAsync(tokenA);

        RideMutationResultDto? received = null;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<RideMutationResultDto>(UpdatesHub.EntitiesAffectedMethod, dto =>
        {
            received = dto;
            tcs.TrySetResult();
        });

        AuthenticateAsUserA();
        var response = await Client.PostAsJsonAsync("/api/rides", new CreateRideDto
        {
            Name = "Manual",
            Distance = 1000,
            StartDateLocal = DateTime.UtcNow,
            BikeId = BikeAId,
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(tcs.Task, completed);
        Assert.NotNull(received);
        Assert.NotEmpty(received!.AffectedRideIds);
    }
}
