using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.DistanceUnits;

/// <summary>
/// ADR 0002 Decision A — metres migration / API contracts (QA A-01–A-06, A-08).
/// </summary>
public class DistanceUnitsMetresMigrationTests : IntegrationTestBase
{
    public DistanceUnitsMetresMigrationTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    // A-01 + A-02 [P0] ×1000 on former-km fields; StravaDistance unchanged; *Km renamed.
    [Fact]
    public async Task Migration_multiplies_km_columns_renames_and_leaves_strava_distance()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = db.GetService<IMigrator>()
            ?? throw new InvalidOperationException("IMigrator is not available from AppDbContext.");

        await db.Database.EnsureDeletedAsync();
        try
        {
            await migrator.MigrateAsync("20260802213000_UniqueExternalServiceUserId");

            var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var bikeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var cycleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var now = DateTime.UtcNow;

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Users" ("Id", "Name", "Email", "PasswordHash", "CreatedAt")
                VALUES ({0}, {1}, {2}, {3}, {4});
                """,
                userId, "User A", "a@example.com", "hash-a", now);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Bikes" ("Id", "UserId", "Name", "Description", "Type", "TotalDistance", "StravaDistance", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9});
                """,
                bikeId, userId, "Bike A", "Owned by A", "Road", 10.0, 10000.0, true, now, now);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "ChainCycles" ("Id", "BikeId", "ChainsJson", "IntervalKm", "CreatedAt", "UpdatedAt")
                VALUES ({0}, {1}, {2}, {3}, {4}, {5});
                """,
                cycleId, bikeId, "[]", 700.0, now, now);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "UserSettings" ("UserId", "DefaultChainCycleLength", "DefaultChainCycleIntervalKm", "defaultUseChainCycle", "showTips", "CreatedAt", "UpdatedAt")
                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6});
                """,
                userId, 3, 700, true, true, now, now);

            await migrator.MigrateAsync();

            var bike = await db.Bikes.AsNoTracking().SingleAsync(b => b.Id == bikeId);
            Assert.Equal(10000.0, bike.TotalDistance);
            Assert.Equal(10000.0, bike.StravaDistance);

            var cycle = await db.ChainCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId);
            Assert.Equal(700000.0, cycle.IntervalMetres);

            var settings = await db.UserSettings.AsNoTracking().SingleAsync(s => s.UserId == userId);
            Assert.Equal(700_000, settings.DefaultChainCycleIntervalMetres);
        }
        finally
        {
            // Leave the shared test DB on the latest schema for subsequent tests.
            await migrator.MigrateAsync();
        }
    }

    // A-05 [P0] GET bike → totalDistance in metres after A.
    [Fact]
    public async Task Get_bike_returns_total_distance_in_metres()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var response = await Client.GetAsync($"/api/bikes/{BikeAId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bike = await ReadJsonAsync<BikeDto>(response);
        Assert.NotNull(bike);
        Assert.Equal(10000.0, bike!.TotalDistance);
        Assert.Equal(10000.0, bike.StravaDistance);
    }

    // A-04 [P0] Chain cycle create/update with intervalMetres; intervalKm absent.
    [Fact]
    public async Task Chain_cycle_persists_interval_metres()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var create = await Client.PostAsJsonAsync("/api/ChainCycles", new CreateChainCycleDto
        {
            BikeId = BikeAId,
            Chains = new List<Guid?> { null, null, null },
            IntervalMetres = 700_000
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await ReadJsonAsync<ChainCycleResponseDto>(create);
        Assert.NotNull(created);
        Assert.Equal(700_000, created!.IntervalMetres);

        var update = await Client.PutAsJsonAsync($"/api/ChainCycles/{created.Id}", new UpdateChainCycleDto
        {
            IntervalMetres = 500_000
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadJsonAsync<ChainCycleResponseDto>(update);
        Assert.NotNull(updated);
        Assert.Equal(500_000, updated!.IntervalMetres);

        // Wire contract: intervalKm must not appear on the response.
        using var doc = JsonDocument.Parse(await ReadBodyAsync(create));
        Assert.False(doc.RootElement.TryGetProperty("intervalKm", out _));
        Assert.True(doc.RootElement.TryGetProperty("intervalMetres", out _));
    }

    // A-06 [P0] New settings default interval is 700_000 metres.
    [Fact]
    public async Task New_user_settings_default_interval_is_700000_metres()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var get = await Client.GetAsync("/api/users/settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var settings = await ReadJsonAsync<UserSettingsDto>(get);
        Assert.NotNull(settings);
        Assert.Equal(700_000, settings!.DefaultChainCycleIntervalMetres);
    }
}

/// <summary>
/// ADR 0002 Decision B — DistanceUnit preference (QA B-06–B-08, B-16).
/// </summary>
public class DistanceUnitSettingsTests : IntegrationTestBase
{
    public DistanceUnitSettingsTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    // B-06 [P0] Null DistanceUnit stays null (inference is client-side).
    [Fact]
    public async Task Null_distance_unit_is_returned_as_null_and_not_written_by_inference()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var get = await Client.GetAsync("/api/users/settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var settings = await ReadJsonAsync<UserSettingsDto>(get);
        Assert.NotNull(settings);
        Assert.Null(settings!.DistanceUnit);
    }

    // B-07 [P0] PUT km | mi | null → 200; garbage → COMMON_VALIDATION.
    [Fact]
    public async Task Put_distance_unit_accepts_km_mi_null_and_rejects_invalid()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        foreach (var unit in new[] { "km", "mi" })
        {
            var put = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { DistanceUnit = unit });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var body = await ReadJsonAsync<UserSettingsDto>(put);
            Assert.Equal(unit, body!.DistanceUnit);
        }

        // Explicit null clears the preference (JSON key present).
        using (var clearContent = new StringContent("""{"distanceUnit":null}""", Encoding.UTF8, "application/json"))
        {
            var clear = await Client.PutAsync("/api/users/settings", clearContent);
            Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
            var cleared = await ReadJsonAsync<UserSettingsDto>(clear);
            Assert.Null(cleared!.DistanceUnit);
        }

        foreach (var garbage in new[] { "meters", "KM", "mile" })
        {
            var bad = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { DistanceUnit = garbage });
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            using var doc = JsonDocument.Parse(await ReadBodyAsync(bad));
            Assert.Equal(ErrorCodes.CommonValidation, doc.RootElement.GetProperty("code").GetString());
        }
    }

    // B-08 [P0] PUT mi then new GET → "mi".
    [Fact]
    public async Task Distance_unit_persists_across_requests()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var put = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { DistanceUnit = "mi" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var get = await Client.GetAsync("/api/users/settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var settings = await ReadJsonAsync<UserSettingsDto>(get);
        Assert.Equal("mi", settings!.DistanceUnit);
    }

    // B-16 [P0] Isolation + anonymous 401.
    [Fact]
    public async Task Distance_unit_is_isolated_per_user_and_requires_auth()
    {
        await SeedTwoUsersWithBikesAsync();

        AuthenticateAsUserA();
        var putA = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { DistanceUnit = "mi" });
        Assert.Equal(HttpStatusCode.OK, putA.StatusCode);

        AuthenticateAsUserB();
        var getB = await Client.GetAsync("/api/users/settings");
        Assert.Equal(HttpStatusCode.OK, getB.StatusCode);
        var settingsB = await ReadJsonAsync<UserSettingsDto>(getB);
        Assert.Null(settingsB!.DistanceUnit);

        ClearAuth();
        var anon = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { DistanceUnit = "km" });
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
    }

    // Partial update of distance unit must not clobber other settings.
    [Fact]
    public async Task Partial_update_of_distance_unit_preserves_other_settings()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var seed = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto
        {
            DefaultChainCycleLength = 2,
            DefaultChainCycleIntervalMetres = 555_000,
            showTips = false,
            Language = "de"
        });
        Assert.Equal(HttpStatusCode.OK, seed.StatusCode);

        var put = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { DistanceUnit = "mi" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await ReadJsonAsync<UserSettingsDto>(put);

        Assert.Equal("mi", updated!.DistanceUnit);
        Assert.Equal(2, updated.DefaultChainCycleLength);
        Assert.Equal(555_000, updated.DefaultChainCycleIntervalMetres);
        Assert.False(updated.showTips);
        Assert.Equal("de", updated.Language);
    }

    // Auth envelope DefaultChainCycleInterval is metres (ADR 0002 E2).
    [Fact]
    public async Task Auth_envelope_exposes_default_chain_cycle_interval_in_metres()
    {
        var register = await Client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            Name = "Interval Rider",
            Email = "interval-auth@example.com",
            Password = "secret12",
            ConfirmPassword = "secret12"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registered = await ReadJsonAsync<AuthResponseDto>(register);
        Assert.NotNull(registered?.User);
        Assert.Equal(3, registered!.User!.DefaultChainCycleLength);
        Assert.Equal(700_000, registered.User.DefaultChainCycleInterval);

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", registered.Token);
        var put = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto
        {
            DefaultChainCycleLength = 2,
            DefaultChainCycleIntervalMetres = 555_000
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        ClearAuth();
        var login = await Client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "interval-auth@example.com",
            Password = "secret12"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loggedIn = await ReadJsonAsync<AuthResponseDto>(login);
        Assert.NotNull(loggedIn?.User);
        Assert.Equal(2, loggedIn!.User!.DefaultChainCycleLength);
        Assert.Equal(555_000, loggedIn.User.DefaultChainCycleInterval);
    }
}
