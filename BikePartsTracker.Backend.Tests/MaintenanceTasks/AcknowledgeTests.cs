using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Localization;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.MaintenanceTasks;

/// <summary>
/// ADR 0011 — acknowledge command (QA BE-01…BE-13, BE-19).
/// </summary>
public class AcknowledgeTests : IntegrationTestBase
{
    private static readonly Guid TaskId = Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa");
    private static readonly Guid PartId = Guid.Parse("bbbbbbbb-2222-2222-2222-bbbbbbbbbbbb");
    private static readonly Guid CycleId = Guid.Parse("cccccccc-3333-3333-3333-cccccccccccc");
    private static readonly Guid ChainPartId = Guid.Parse("dddddddd-4444-4444-4444-dddddddddddd");

    public AcknowledgeTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    private async Task<User> GetUserAAsync(AppDbContext db) =>
        await db.Users.FirstAsync(u => u.Id == UserAId);

    private static void SeedPart(AppDbContext db, User user, Guid? bikeId = null)
    {
        db.BikeParts.Add(new BikePart
        {
            Id = PartId,
            UserId = UserAId,
            User = user,
            Name = "Chain",
            PartType = PartType.Chain,
            Type = PartType.Chain.ToString(),
            IsActive = true,
            BikeId = bikeId ?? BikeAId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private async Task<(Guid taskId, DateTime startDate)> SeedTaskAsync(
        MaintenanceTaskType type,
        MaintenanceTaskTriggerType triggerType,
        double triggerValue,
        DateTime startDate,
        bool isActive = true,
        MaintenanceTaskParentType parentType = MaintenanceTaskParentType.Bike,
        Guid? parentId = null,
        Guid? taskId = null)
    {
        await SeedTwoUsersWithBikesAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await GetUserAAsync(db);

        if (parentType == MaintenanceTaskParentType.Part)
        {
            SeedPart(db, user);
        }

        var id = taskId ?? TaskId;
        db.MaintenanceTasks.Add(new MaintenanceTask
        {
            Id = id,
            UserId = UserAId,
            User = user,
            Name = $"{type} task",
            StartDate = startDate,
            Type = type,
            TriggerType = triggerType,
            ParentType = parentType,
            ParentId = parentId ?? (parentType == MaintenanceTaskParentType.Part ? PartId : BikeAId),
            TriggerValue = triggerValue,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (id, startDate);
    }

    private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, doc.RootElement.GetProperty("code").GetString());
    }

    // BE-01 [P0]
    [Fact]
    public async Task Acknowledge_repeating_due_resets_window()
    {
        var start = DateTime.UtcNow.AddDays(-10);
        await SeedTaskAsync(MaintenanceTaskType.Repeating, MaintenanceTaskTriggerType.Time, 5, start);
        AuthenticateAsUserA();

        var before = DateTime.UtcNow.AddSeconds(-5);
        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto { Force = false });
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<AcknowledgeMaintenanceTaskResponseDto>(response);
        Assert.NotNull(body);
        Assert.True(body!.MaintenanceTask.IsActive);
        Assert.False(body.MaintenanceTask.NeedsAttention);
        Assert.True(body.MaintenanceTask.ConsumedValue < 0.01);
        Assert.Contains(TaskId, body.Affected.AffectedMaintenanceTaskIds);
        Assert.Empty(body.Affected.AffectedRideIds);
        Assert.Empty(body.Affected.AffectedPartIds);
        Assert.Empty(body.Affected.AffectedBikeIds);
        Assert.InRange(body.MaintenanceTask.StartDate, before, after);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.MaintenanceTasks.SingleAsync(t => t.Id == TaskId);
        Assert.True(task.IsActive);
        Assert.InRange(task.StartDate, before, after);
    }

    // BE-02 [P0]
    [Fact]
    public async Task Acknowledge_cyclic_due_resets_window_without_swap()
    {
        await SeedTwoUsersWithBikesAsync();
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await GetUserAAsync(db);
            db.BikeParts.Add(new BikePart
            {
                Id = ChainPartId,
                UserId = UserAId,
                User = user,
                Name = "Active chain",
                PartType = PartType.Chain,
                Type = PartType.Chain.ToString(),
                IsActive = true,
                BikeId = BikeAId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.ChainCycles.Add(new ChainCycle
            {
                Id = CycleId,
                BikeId = BikeAId,
                Chains = new List<Guid?> { ChainPartId, null, null },
                ActiveChainId = ChainPartId,
                IntervalMetres = 700_000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.MaintenanceTasks.Add(new MaintenanceTask
            {
                Id = TaskId,
                UserId = UserAId,
                User = user,
                Name = "Rotate",
                StartDate = DateTime.UtcNow.AddDays(-10),
                Type = MaintenanceTaskType.Cyclic,
                TriggerType = MaintenanceTaskTriggerType.Time,
                ParentType = MaintenanceTaskParentType.ChainCycle,
                ParentId = CycleId,
                TriggerValue = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        AuthenticateAsUserA();
        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<AcknowledgeMaintenanceTaskResponseDto>(response);
        Assert.NotNull(body);
        Assert.True(body!.MaintenanceTask.IsActive);
        Assert.False(body.MaintenanceTask.NeedsAttention);

        await using var verify = Fixture.Factory.Services.CreateAsyncScope();
        var db2 = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var cycle = await db2.ChainCycles.SingleAsync(c => c.Id == CycleId);
        Assert.Equal(ChainPartId, cycle.ActiveChainId); // no swap (ADR 0012 out of scope)
    }

    // BE-03 [P0]
    [Fact]
    public async Task Acknowledge_onetime_due_deactivates_without_changing_start()
    {
        var start = DateTime.UtcNow.AddDays(-10);
        await SeedTaskAsync(MaintenanceTaskType.OneTime, MaintenanceTaskTriggerType.Time, 5, start);
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto { Force = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<AcknowledgeMaintenanceTaskResponseDto>(response);
        Assert.NotNull(body);
        Assert.False(body!.MaintenanceTask.IsActive);
        Assert.Equal(start, body.MaintenanceTask.StartDate, TimeSpan.FromSeconds(1));

        var list = await Client.GetFromJsonAsync<List<MaintenanceTaskDto>>(
            "/api/maintenance-tasks?isActive=true",
            JsonOptions);
        Assert.NotNull(list);
        Assert.DoesNotContain(list!, t => t.Id == TaskId);
    }

    // BE-04 [P0]
    [Fact]
    public async Task Acknowledge_early_without_force_returns_400()
    {
        var start = DateTime.UtcNow;
        await SeedTaskAsync(MaintenanceTaskType.Repeating, MaintenanceTaskTriggerType.Time, 30, start);
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto { Force = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.MaintenanceTaskNotDue);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.MaintenanceTasks.SingleAsync(t => t.Id == TaskId);
        Assert.Equal(start, task.StartDate, TimeSpan.FromSeconds(1));
        Assert.True(task.IsActive);
    }

    // BE-05 [P0]
    [Fact]
    public async Task Acknowledge_early_with_force_succeeds()
    {
        await SeedTaskAsync(MaintenanceTaskType.Repeating, MaintenanceTaskTriggerType.Time, 30, DateTime.UtcNow);
        AuthenticateAsUserA();

        var before = DateTime.UtcNow.AddSeconds(-5);
        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto { Force = true });
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<AcknowledgeMaintenanceTaskResponseDto>(response);
        Assert.NotNull(body);
        Assert.True(body!.MaintenanceTask.IsActive);
        Assert.InRange(body.MaintenanceTask.StartDate, before, after);
    }

    // BE-06 [P0]
    [Fact]
    public async Task Acknowledge_completed_onetime_returns_409()
    {
        await SeedTaskAsync(
            MaintenanceTaskType.OneTime,
            MaintenanceTaskTriggerType.Time,
            5,
            DateTime.UtcNow.AddDays(-10),
            isActive: false);
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto { Force = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.MaintenanceTaskAlreadyCompleted);
    }

    // BE-07 [P0]
    [Fact]
    public async Task Acknowledge_inactive_repeating_returns_409()
    {
        await SeedTaskAsync(
            MaintenanceTaskType.Repeating,
            MaintenanceTaskTriggerType.Time,
            5,
            DateTime.UtcNow.AddDays(-10),
            isActive: false);
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto { Force = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemCodeAsync(response, ErrorCodes.MaintenanceTaskInactive);
    }

    // BE-08 [P0]
    [Fact]
    public async Task Acknowledge_other_users_task_returns_404()
    {
        await SeedTaskAsync(MaintenanceTaskType.Repeating, MaintenanceTaskTriggerType.Time, 5, DateTime.UtcNow.AddDays(-10));
        AuthenticateAsUserB();

        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // BE-09 [P0]
    [Fact]
    public async Task Acknowledge_unauthenticated_returns_401()
    {
        await SeedTaskAsync(MaintenanceTaskType.Repeating, MaintenanceTaskTriggerType.Time, 5, DateTime.UtcNow.AddDays(-10));
        ClearAuth();

        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // BE-10 [P0]
    [Fact]
    public async Task Acknowledge_part_distance_repeating_resyncs_shadows()
    {
        await SeedTwoUsersWithBikesAsync();
        var oldStart = DateTime.UtcNow.AddDays(-14);
        var usageStart = DateTime.UtcNow.AddDays(-20);
        Guid shadowId;

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await GetUserAAsync(db);
            SeedPart(db, user);

            var realPeriod = new PartUsageHistory
            {
                Id = Guid.NewGuid(),
                BikePartId = PartId,
                BikePart = null!,
                BikeId = BikeAId,
                StartDate = usageStart,
                EndDate = null,
                Distance = 50_000,
                IsShadow = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.PartUsageHistories.Add(realPeriod);

            db.MaintenanceTasks.Add(new MaintenanceTask
            {
                Id = TaskId,
                UserId = UserAId,
                User = user,
                Name = "Lube",
                StartDate = oldStart,
                Type = MaintenanceTaskType.Repeating,
                TriggerType = MaintenanceTaskTriggerType.Distance,
                ParentType = MaintenanceTaskParentType.Part,
                ParentId = PartId,
                TriggerValue = 1, // already "due" via shadow/real distance
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            shadowId = Guid.NewGuid();
            db.PartUsageHistories.Add(new PartUsageHistory
            {
                Id = shadowId,
                BikePartId = PartId,
                BikePart = null!,
                BikeId = BikeAId,
                MaintenanceTaskId = TaskId,
                SourceUsagePeriodId = realPeriod.Id,
                StartDate = oldStart,
                EndDate = null,
                Distance = 40_000,
                IsShadow = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        AuthenticateAsUserA();
        var before = DateTime.UtcNow.AddSeconds(-5);
        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto());
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verify = Fixture.Factory.Services.CreateAsyncScope();
        var db2 = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db2.PartUsageHistories.AnyAsync(h => h.Id == shadowId));

        var shadows = await db2.PartUsageHistories
            .Where(h => h.MaintenanceTaskId == TaskId && h.IsShadow)
            .ToListAsync();
        Assert.Single(shadows);
        Assert.InRange(shadows[0].StartDate, before, after);
    }

    // BE-11 [P1]
    [Fact]
    public async Task Acknowledge_due_with_force_true_still_ok()
    {
        await SeedTaskAsync(MaintenanceTaskType.Repeating, MaintenanceTaskTriggerType.Time, 5, DateTime.UtcNow.AddDays(-10));
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto { Force = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // BE-12 [P1]
    [Fact]
    public async Task Acknowledge_repeating_then_immediate_reack_without_force_returns_400()
    {
        await SeedTaskAsync(MaintenanceTaskType.Repeating, MaintenanceTaskTriggerType.Time, 5, DateTime.UtcNow.AddDays(-10));
        AuthenticateAsUserA();

        var first = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto { Force = false });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        await AssertProblemCodeAsync(second, ErrorCodes.MaintenanceTaskNotDue);
    }

    // BE-13 [P1] covered by BE-01 envelope asserts

    // BE-19 [P1]
    [Fact]
    public async Task Acknowledge_time_repeating_resets_days_without_shadows()
    {
        await SeedTaskAsync(MaintenanceTaskType.Repeating, MaintenanceTaskTriggerType.Time, 5, DateTime.UtcNow.AddDays(-10));
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/maintenance-tasks/{TaskId}/acknowledge",
            new AcknowledgeMaintenanceTaskDto());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<AcknowledgeMaintenanceTaskResponseDto>(response);
        Assert.NotNull(body);
        Assert.True(body!.MaintenanceTask.ConsumedValue < 0.01);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.PartUsageHistories.CountAsync(h => h.MaintenanceTaskId == TaskId));
    }
}
