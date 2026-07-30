using System.Net;
using System.Net.Http.Json;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.DTOs;
using Xunit;

namespace BikePartsTracker.Backend.Tests.Settings;

/// <summary>
/// UserSettings.Language field + isolation (ADR 0006 §Prereq, Accepted; ADR 0003 scoping).
///
/// Implemented alongside the settings increment: the persisted <c>Language</c> (BCP-47) field on
/// UserSettings, exposed on <see cref="UserSettingsDto"/> and settable via
/// <see cref="UpdateUserSettingsDto"/> as a partial update, scoped per-user like the rest of settings.
/// </summary>
public class UserSettingsLanguageTests : IntegrationTestBase
{
    public UserSettingsLanguageTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    // B-10 [P0] User A sets Language=de; user B GET never sees A's value (ADR 0003 isolation).
    [Fact]
    public async Task One_users_language_is_not_visible_to_another_user()
    {
        await SeedTwoUsersWithBikesAsync();

        AuthenticateAsUserA();
        var putA = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { Language = "de" });
        Assert.Equal(HttpStatusCode.OK, putA.StatusCode);

        AuthenticateAsUserB();
        var getB = await Client.GetAsync("/api/users/settings");
        Assert.Equal(HttpStatusCode.OK, getB.StatusCode);
        var settingsB = await ReadJsonAsync<UserSettingsDto>(getB);

        Assert.NotNull(settingsB);
        Assert.Null(settingsB!.Language);
    }

    // B-11 [P0] Anonymous PUT /api/users/settings {language} → 401 (FallbackPolicy).
    [Fact]
    public async Task Anonymous_put_settings_language_returns_401()
    {
        ClearAuth();

        var response = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { Language = "de" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // B-12 [P1] PUT {language:"ru"} partial update leaves chain-cycle defaults untouched.
    [Fact]
    public async Task Partial_update_of_language_does_not_clobber_other_settings()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        // Seed a known non-default chain-cycle value.
        var seed = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto
        {
            DefaultChainCycleLength = 2,
            DefaultChainCycleIntervalKm = 555,
            showTips = false
        });
        Assert.Equal(HttpStatusCode.OK, seed.StatusCode);

        // Now update only the language.
        var put = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { Language = "ru" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await ReadJsonAsync<UserSettingsDto>(put);

        Assert.NotNull(updated);
        Assert.Equal("ru", updated!.Language);
        Assert.Equal(2, updated.DefaultChainCycleLength);
        Assert.Equal(555, updated.DefaultChainCycleIntervalKm);
        Assert.False(updated.showTips);
    }

    // B-13 [P1] Language=null → GET absent/null, documented to resolve to English (no server default write).
    [Fact]
    public async Task Null_language_is_returned_as_null_and_resolves_to_english()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var get = await Client.GetAsync("/api/users/settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var settings = await ReadJsonAsync<UserSettingsDto>(get);

        Assert.NotNull(settings);
        Assert.Null(settings!.Language);
    }
}
