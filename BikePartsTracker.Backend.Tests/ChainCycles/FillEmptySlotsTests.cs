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

namespace BikePartsTracker.Backend.Tests.ChainCycles;

/// <summary>
/// ADR 0010 — fill-empty-slots (QA BE-01…BE-12).
/// </summary>
public class FillEmptySlotsTests : IntegrationTestBase
{
    private static readonly Guid CycleAId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ExistingChain0Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid ExistingChain2Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    public FillEmptySlotsTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    private async Task<(Guid cycleId, Guid? existingActiveId)> SeedCycleAsync(
        List<Guid?> chains,
        Guid? activeChainId = null,
        string bikeName = "Bike A")
    {
        await SeedTwoUsersWithBikesAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var bike = await db.Bikes.FirstAsync(b => b.Id == BikeAId);
        bike.Name = bikeName;
        bike.TotalDistance = 12_345;

        // Materialize any pre-existing chain parts referenced in slots.
        foreach (var id in chains.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
        {
            if (!await db.BikeParts.AnyAsync(p => p.Id == id))
            {
                db.BikeParts.Add(new BikePart
                {
                    Id = id,
                    UserId = UserAId,
                    BikeId = BikeAId,
                    Name = $"Existing {id.ToString()[..8]}",
                    PartType = PartType.Chain,
                    Type = PartType.Chain.ToString(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        var cycle = new ChainCycle
        {
            Id = CycleAId,
            BikeId = BikeAId,
            Chains = chains,
            ActiveChainId = activeChainId,
            IntervalMetres = 700_000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ChainCycles.Add(cycle);
        await db.SaveChangesAsync();
        return (cycle.Id, activeChainId);
    }

    private async Task OpenUsageForAsync(Guid partId, Guid bikeId, DateTime start)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var usage = scope.ServiceProvider.GetRequiredService<BikePartsTracker.Services.IPartUsageTrackingService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var part = await db.BikeParts.FirstAsync(p => p.Id == partId);
        await usage.OpenUsagePeriodAsync(part, bikeId, start);
    }

    // BE-01 [P0]
    [Fact]
    public async Task Fill_with_existing_active_fills_empty_only_without_new_usage()
    {
        await SeedCycleAsync(
            new List<Guid?> { ExistingChain0Id, null, null },
            activeChainId: ExistingChain0Id);
        await OpenUsageForAsync(ExistingChain0Id, BikeAId, DateTime.UtcNow.AddDays(-1));
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 1, InstallationDate = DateTime.UtcNow.AddDays(-30) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<FillEmptyChainCycleSlotsResponseDto>(response);
        Assert.NotNull(body);
        Assert.Equal(2, body!.CreatedParts.Count);
        Assert.Equal(ExistingChain0Id, body.ChainCycle.ActiveChainId);
        Assert.Equal(ExistingChain0Id, body.ChainCycle.Chains[0]);
        Assert.NotNull(body.ChainCycle.Chains[1]);
        Assert.NotNull(body.ChainCycle.Chains[2]);
        Assert.Contains(body.CreatedParts, p => p.Name == "Bike A chain 2");
        Assert.Contains(body.CreatedParts, p => p.Name == "Bike A chain 3");
        Assert.All(body.CreatedParts, p => Assert.Equal(BikeAId, p.BikeId));
        Assert.Equal(2, body.AffectedPartIds.Count);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createdIds = body.CreatedParts.Select(p => p.Id).ToList();
        var openForCreated = await db.PartUsageHistories
            .CountAsync(h => createdIds.Contains(h.BikePartId) && h.EndDate == null && !h.IsShadow);
        Assert.Equal(0, openForCreated);
        var openForActive = await db.PartUsageHistories
            .CountAsync(h => h.BikePartId == ExistingChain0Id && h.EndDate == null && !h.IsShadow);
        Assert.Equal(1, openForActive);
    }

    // BE-02 [P0]
    [Fact]
    public async Task Fill_activate_first_new_slot_opens_usage_only_for_that_chain()
    {
        await SeedCycleAsync(new List<Guid?> { null, null, null });
        AuthenticateAsUserA();

        var before = DateTime.UtcNow.AddSeconds(-5);
        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 0 });
        var after = DateTime.UtcNow.AddSeconds(5);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<FillEmptyChainCycleSlotsResponseDto>(response);
        Assert.NotNull(body);
        Assert.Equal(3, body!.CreatedParts.Count);
        var activeId = body.ChainCycle.Chains[0];
        Assert.Equal(activeId, body.ChainCycle.ActiveChainId);
        Assert.NotNull(activeId);

        var activePart = body.CreatedParts.Single(p => p.Id == activeId);
        Assert.NotNull(activePart.InstallationDate);
        Assert.InRange(activePart.InstallationDate!.Value, before, after);
        Assert.Equal(12_345, activePart.MileageAtInstallation);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var openPeriods = await db.PartUsageHistories
            .Where(h => h.EndDate == null && !h.IsShadow)
            .ToListAsync();
        Assert.Single(openPeriods);
        Assert.Equal(activeId, openPeriods[0].BikePartId);
        Assert.InRange(openPeriods[0].StartDate, before, after);

        foreach (var other in body.CreatedParts.Where(p => p.Id != activeId))
        {
            Assert.Equal(BikeAId, other.BikeId);
            Assert.Null(other.InstallationDate);
        }
    }

    // BE-03 [P0]
    [Fact]
    public async Task Fill_activate_last_new_slot_only()
    {
        await SeedCycleAsync(new List<Guid?> { null, null, null });
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 2 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<FillEmptyChainCycleSlotsResponseDto>(response);
        Assert.NotNull(body);
        var activeId = body!.ChainCycle.Chains[2];
        Assert.Equal(activeId, body.ChainCycle.ActiveChainId);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var openIds = await db.PartUsageHistories
            .Where(h => h.EndDate == null && !h.IsShadow)
            .Select(h => h.BikePartId)
            .ToListAsync();
        Assert.Equal(new[] { activeId!.Value }, openIds);
    }

    // BE-04 [P0]
    [Fact]
    public async Task Fill_none_yet_leaves_no_active_and_no_usage()
    {
        await SeedCycleAsync(new List<Guid?> { null, null, null });
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<FillEmptyChainCycleSlotsResponseDto>(response);
        Assert.NotNull(body);
        Assert.Null(body!.ChainCycle.ActiveChainId);
        Assert.All(body.ChainCycle.Chains, id => Assert.NotNull(id));
        Assert.All(body.CreatedParts, p => Assert.Equal(BikeAId, p.BikeId));

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.PartUsageHistories.CountAsync(h => !h.IsShadow));
    }

    // BE-05 [P0]
    [Fact]
    public async Task Fill_with_past_install_opens_usage_at_that_time_and_enqueues_gap_fill()
    {
        await SeedCycleAsync(new List<Guid?> { null, null });
        var tPast = DateTime.UtcNow.Date.AddDays(-10);
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Rides.Add(new Ride
            {
                Id = Guid.NewGuid(),
                UserId = UserAId,
                User = null!,
                BikeId = BikeAId,
                Name = "Past ride",
                Type = "Ride",
                Distance = 5000,
                RecordedDistance = 5000,
                StartDateLocal = tPast.AddDays(1),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.ExternalServiceIntegrations.Add(new ExternalServiceIntegration
            {
                Id = Guid.NewGuid(),
                UserId = UserAId,
                User = null!,
                ServiceType = ExternalServiceType.Strava,
                ServiceUserId = "10001",
                AccessToken = "access",
                RefreshToken = "refresh",
                TokenExpiry = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        AuthenticateAsUserA();
        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto
            {
                ActiveNewSlotIndex = 0,
                InstallationDate = tPast
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<FillEmptyChainCycleSlotsResponseDto>(response);
        Assert.NotNull(body);

        await using var verify = Fixture.Factory.Services.CreateAsyncScope();
        var db2 = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var period = await db2.PartUsageHistories
            .SingleAsync(h => h.BikePartId == body!.ChainCycle.ActiveChainId && !h.IsShadow);
        Assert.Equal(tPast, period.StartDate);

        var queue = Fixture.Factory.Services.GetRequiredService<BikePartsTracker.BackgroundJobs.IBackgroundJobQueue>();
        Assert.True(queue.TryDequeue(out var job));
        Assert.Equal(BikePartsTracker.BackgroundJobs.BackgroundJobKind.GapFillAutoImport, job!.Kind);
    }

    // BE-06 [P0]
    [Fact]
    public async Task Fill_partial_cycle_only_fills_middle_slot_with_slot_index_name()
    {
        await SeedCycleAsync(
            new List<Guid?> { ExistingChain0Id, null, ExistingChain2Id },
            activeChainId: ExistingChain0Id);
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<FillEmptyChainCycleSlotsResponseDto>(response);
        Assert.NotNull(body);
        Assert.Single(body!.CreatedParts);
        Assert.Equal("Bike A chain 2", body.CreatedParts[0].Name);
        Assert.Equal(ExistingChain0Id, body.ChainCycle.Chains[0]);
        Assert.Equal(body.CreatedParts[0].Id, body.ChainCycle.Chains[1]);
        Assert.Equal(ExistingChain2Id, body.ChainCycle.Chains[2]);
        Assert.Equal(ExistingChain0Id, body.ChainCycle.ActiveChainId);
    }

    // BE-07 [P1]
    [Fact]
    public async Task Fill_when_no_empty_slots_returns_400_stable_code()
    {
        await SeedCycleAsync(
            new List<Guid?> { ExistingChain0Id, ExistingChain2Id },
            activeChainId: ExistingChain0Id);
        AuthenticateAsUserA();

        var partsBefore = await CountPartsAsync();
        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await ReadBodyAsync(response));
        Assert.Equal(ErrorCodes.ChainCyclesNoEmptySlots, doc.RootElement.GetProperty("code").GetString());
        Assert.Equal(partsBefore, await CountPartsAsync());
    }

    // BE-08 [P0]
    [Fact]
    public async Task Fill_other_users_cycle_is_forbidden()
    {
        await SeedCycleAsync(new List<Guid?> { null, null });
        AuthenticateAsUserB();

        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountPartsAsync());
    }

    // BE-09 [P0]
    [Fact]
    public async Task Fill_rolls_back_when_fault_after_first_part()
    {
        await SeedCycleAsync(new List<Guid?> { null, null, null });
        Fixture.Factory.FillFaultInjector.ThrowAfter(1);
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 0 });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await CountPartsAsync());

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cycle = await db.ChainCycles.AsNoTracking().SingleAsync(c => c.Id == CycleAId);
        Assert.All(cycle.Chains, id => Assert.Null(id));
        Assert.Null(cycle.ActiveChainId);
    }

    // BE-10 [P1]
    [Fact]
    public async Task Fill_with_existing_active_ignores_client_active_and_install_fields()
    {
        await SeedCycleAsync(
            new List<Guid?> { ExistingChain0Id, null },
            activeChainId: ExistingChain0Id);
        await OpenUsageForAsync(ExistingChain0Id, BikeAId, DateTime.UtcNow.AddDays(-2));
        AuthenticateAsUserA();

        var past = DateTime.UtcNow.AddDays(-40);
        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 1, InstallationDate = past });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<FillEmptyChainCycleSlotsResponseDto>(response);
        Assert.Equal(ExistingChain0Id, body!.ChainCycle.ActiveChainId);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createdId = body.CreatedParts.Single().Id;
        Assert.False(await db.PartUsageHistories.AnyAsync(h => h.BikePartId == createdId && !h.IsShadow));
        var activePeriod = await db.PartUsageHistories
            .SingleAsync(h => h.BikePartId == ExistingChain0Id && h.EndDate == null && !h.IsShadow);
        Assert.True(activePeriod.StartDate > past.AddDays(30)); // still the original ~2 days ago, not past
    }

    // BE-11 [P1]
    [Fact]
    public async Task Fill_invalid_active_slot_returns_400()
    {
        await SeedCycleAsync(
            new List<Guid?> { ExistingChain0Id, null, null });
        AuthenticateAsUserA();

        // Slot 0 is already filled — cannot activate via this field when no ActiveChainId... 
        // Cycle has no active, but slot 0 is filled: activeNewSlotIndex must address an empty slot.
        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await ReadBodyAsync(response));
        Assert.Equal(ErrorCodes.ChainCyclesInvalidActiveSlot, doc.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, await CountPartsAsync()); // only the seeded existing chain

        var oob = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, oob.StatusCode);
    }

    // BE-12 [P1]
    [Fact]
    public async Task Fill_response_is_dto_envelope_with_affected_ids()
    {
        await SeedCycleAsync(new List<Guid?> { null, null });
        AuthenticateAsUserA();

        var response = await Client.PostAsJsonAsync(
            $"/api/chaincycles/{CycleAId}/fill-empty-slots",
            new FillEmptyChainCycleSlotsDto { ActiveNewSlotIndex = 0 });
        var raw = await ReadBodyAsync(response);
        Assert.DoesNotContain("ChainsJson", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HistoryJson", raw, StringComparison.OrdinalIgnoreCase);

        var body = await ReadJsonAsync<FillEmptyChainCycleSlotsResponseDto>(response);
        Assert.NotNull(body);
        Assert.Equal(2, body!.CreatedParts.Count);
        Assert.Equal(2, body.AffectedPartIds.Count);
        Assert.All(body.CreatedParts.Select(p => p.Id), id => Assert.Contains(id, body.AffectedPartIds));
        Assert.Contains(body.ChainCycle.ActiveChainId!.Value, body.AffectedPartIds);
    }

    private async Task<int> CountPartsAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BikeParts.CountAsync();
    }
}
