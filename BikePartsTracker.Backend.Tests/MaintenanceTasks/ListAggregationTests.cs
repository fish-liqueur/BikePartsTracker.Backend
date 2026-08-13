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
/// ADR 0011 — list aggregation query params (QA BE-14…BE-18).
/// </summary>
public class ListAggregationTests : IntegrationTestBase
{
    private static readonly Guid BikeTaskId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid PartTaskId = Guid.Parse("a2222222-2222-2222-2222-222222222222");
    private static readonly Guid CycleTaskId = Guid.Parse("a3333333-3333-3333-3333-333333333333");
    private static readonly Guid CompletedOneTimeId = Guid.Parse("a4444444-4444-4444-4444-444444444444");
    private static readonly Guid PartId = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherPartId = Guid.Parse("b2222222-2222-2222-2222-222222222222");
    private static readonly Guid CycleId = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    private static readonly Guid ForeignBikeId = Guid.Parse("22222222-2222-2222-2222-222222222222"); // BikeBId

    public ListAggregationTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    private async Task SeedSurfaceFixtureAsync()
    {
        await SeedTwoUsersWithBikesAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == UserAId);

        db.BikeParts.AddRange(
            new BikePart
            {
                Id = PartId,
                UserId = UserAId,
                User = user,
                Name = "Chain A",
                PartType = PartType.Chain,
                Type = PartType.Chain.ToString(),
                IsActive = true,
                BikeId = BikeAId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new BikePart
            {
                Id = OtherPartId,
                UserId = UserAId,
                User = user,
                Name = "Pad",
                PartType = PartType.BrakePads,
                Type = PartType.BrakePads.ToString(),
                IsActive = true,
                BikeId = BikeAId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        db.ChainCycles.Add(new ChainCycle
        {
            Id = CycleId,
            BikeId = BikeAId,
            Chains = new List<Guid?> { PartId, null, null },
            ActiveChainId = PartId,
            IntervalMetres = 700_000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        void AddTask(Guid id, string name, MaintenanceTaskParentType parentType, Guid parentId, bool isActive = true)
        {
            db.MaintenanceTasks.Add(new MaintenanceTask
            {
                Id = id,
                UserId = UserAId,
                User = user,
                Name = name,
                StartDate = DateTime.UtcNow.AddDays(-1),
                Type = isActive ? MaintenanceTaskType.Repeating : MaintenanceTaskType.OneTime,
                TriggerType = MaintenanceTaskTriggerType.Time,
                ParentType = parentType,
                ParentId = parentId,
                TriggerValue = 30,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        AddTask(BikeTaskId, "Bike service", MaintenanceTaskParentType.Bike, BikeAId);
        AddTask(PartTaskId, "Pad clean", MaintenanceTaskParentType.Part, OtherPartId);
        AddTask(CycleTaskId, "Rotate", MaintenanceTaskParentType.ChainCycle, CycleId);
        AddTask(CompletedOneTimeId, "Done once", MaintenanceTaskParentType.Bike, BikeAId, isActive: false);

        await db.SaveChangesAsync();
    }

    // BE-14 [P0]
    [Fact]
    public async Task Get_isActive_filters_open_and_completed()
    {
        await SeedSurfaceFixtureAsync();
        AuthenticateAsUserA();

        var active = await Client.GetFromJsonAsync<List<MaintenanceTaskDto>>(
            "/api/maintenance-tasks?isActive=true",
            JsonOptions);
        Assert.NotNull(active);
        Assert.Contains(active!, t => t.Id == BikeTaskId);
        Assert.DoesNotContain(active!, t => t.Id == CompletedOneTimeId);

        var inactive = await Client.GetFromJsonAsync<List<MaintenanceTaskDto>>(
            "/api/maintenance-tasks?isActive=false",
            JsonOptions);
        Assert.NotNull(inactive);
        Assert.Contains(inactive!, t => t.Id == CompletedOneTimeId);
        Assert.DoesNotContain(inactive!, t => t.Id == BikeTaskId);
    }

    // BE-15 [P0]
    [Fact]
    public async Task Get_bikeId_aggregates_bike_part_and_cycle_tasks()
    {
        await SeedSurfaceFixtureAsync();
        AuthenticateAsUserA();

        var list = await Client.GetFromJsonAsync<List<MaintenanceTaskDto>>(
            $"/api/maintenance-tasks?bikeId={BikeAId}",
            JsonOptions);
        Assert.NotNull(list);
        var ids = list!.Select(t => t.Id).ToHashSet();
        Assert.Contains(BikeTaskId, ids);
        Assert.Contains(PartTaskId, ids);
        Assert.Contains(CycleTaskId, ids);

        var foreign = await Client.GetAsync($"/api/maintenance-tasks?bikeId={ForeignBikeId}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    // BE-16 [P0]
    [Fact]
    public async Task Get_bikeId_excludePartParents_omits_part_rows()
    {
        await SeedSurfaceFixtureAsync();
        AuthenticateAsUserA();

        var list = await Client.GetFromJsonAsync<List<MaintenanceTaskDto>>(
            $"/api/maintenance-tasks?bikeId={BikeAId}&excludePartParents=true",
            JsonOptions);
        Assert.NotNull(list);
        var ids = list!.Select(t => t.Id).ToHashSet();
        Assert.Contains(BikeTaskId, ids);
        Assert.Contains(CycleTaskId, ids);
        Assert.DoesNotContain(PartTaskId, ids);
    }

    // BE-17 [P0]
    [Fact]
    public async Task Get_relatedToPartId_includes_part_and_cycle_containing_part()
    {
        await SeedSurfaceFixtureAsync();
        AuthenticateAsUserA();

        // PartId is in the cycle; also add a Part-parented task for PartId
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == UserAId);
            var chainTaskId = Guid.Parse("a5555555-5555-5555-5555-555555555555");
            db.MaintenanceTasks.Add(new MaintenanceTask
            {
                Id = chainTaskId,
                UserId = UserAId,
                User = user,
                Name = "Chain lube",
                StartDate = DateTime.UtcNow,
                Type = MaintenanceTaskType.Repeating,
                TriggerType = MaintenanceTaskTriggerType.Time,
                ParentType = MaintenanceTaskParentType.Part,
                ParentId = PartId,
                TriggerValue = 30,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var list = await Client.GetFromJsonAsync<List<MaintenanceTaskDto>>(
            $"/api/maintenance-tasks?relatedToPartId={PartId}",
            JsonOptions);
        Assert.NotNull(list);
        var ids = list!.Select(t => t.Id).ToHashSet();
        Assert.Contains(Guid.Parse("a5555555-5555-5555-5555-555555555555"), ids);
        Assert.Contains(CycleTaskId, ids);
        Assert.DoesNotContain(PartTaskId, ids); // OtherPartId-parented
        Assert.DoesNotContain(BikeTaskId, ids);

        var foreignPart = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var foreign = await Client.GetAsync($"/api/maintenance-tasks?relatedToPartId={foreignPart}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    // BE-18 [P0]
    [Fact]
    public async Task Get_bikeId_and_relatedToPartId_together_returns_400()
    {
        await SeedSurfaceFixtureAsync();
        AuthenticateAsUserA();

        var response = await Client.GetAsync(
            $"/api/maintenance-tasks?bikeId={BikeAId}&relatedToPartId={PartId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.CommonValidation, doc.RootElement.GetProperty("code").GetString());
    }
}
