using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BikePartsTracker.Backend.Localization;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.DTOs;
using BikePartsTracker.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BikePartsTracker.Backend.Tests.Localization;

/// <summary>
/// Localization API contract &amp; culture resolution (ADR 0006, Accepted; design E1–E5).
///
/// Implemented alongside the localization increment: RequestLocalizationMiddleware (Accept-Language →
/// culture, unknown → en), the backend .resx catalog keyed by error code, and the global
/// AppException → RFC 9457 Problem Details mapping with a localized <c>detail</c>.
///
/// Note: the ADR's illustrative example uses <c>PARTS_BATCH_LIMIT_EXCEEDED {max:50}</c>; the live
/// limit is 200, so params/detail are asserted against 200 (the mechanism, not the number, is the point).
/// </summary>
public class LocalizationApiTests : IntegrationTestBase
{
    public LocalizationApiTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    // B-01 [P0] Accept-Language: de → known error → detail German, code/params unchanged.
    // B-02 [P0] Accept-Language: fr (unsupported) → culture falls back to en, English detail, no 500.
    // B-03 [P0] No Accept-Language → DefaultRequestCulture = en.
    [Theory]
    [InlineData("de", "Die angeforderte Ressource wurde nicht gefunden.")]
    [InlineData("fr", "The requested resource was not found.")]
    [InlineData("", "The requested resource was not found.")]
    public async Task Culture_is_resolved_from_accept_language_with_english_fallback(string acceptLanguage, string expectedDetail)
    {
        AuthenticateAsUserA();
        if (!string.IsNullOrEmpty(acceptLanguage))
        {
            Client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        }

        var response = await Client.GetAsync($"/api/parts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var doc = JsonDocument.Parse(await ReadBodyAsync(response));
        Assert.Equal(ErrorCodes.CommonNotFound, doc.RootElement.GetProperty("code").GetString());
        Assert.Equal(expectedDetail, doc.RootElement.GetProperty("detail").GetString());
    }

    // B-05 [P0] Catalog: code present in en, missing in de → English (neutral) returned, no throw.
    [Fact]
    public void Catalog_falls_back_to_english_when_translation_missing()
    {
        using var scope = Fixture.Factory.Services.CreateScope();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<ErrorMessages>>();

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            // COMMON_UNEXPECTED is intentionally only in the neutral (English) resource.
            CultureInfo.CurrentUICulture = new CultureInfo("de");
            var localized = localizer[ErrorCodes.CommonUnexpected];

            Assert.False(localized.ResourceNotFound);
            Assert.Equal("An unexpected error occurred. Please try again.", localized.Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    // B-06 [P0] AppException("PARTS_BATCH_LIMIT_EXCEEDED",{max}) → RFC 9457 body with code,
    //           params.max, interpolated detail, correct status.
    [Fact]
    public async Task AppException_maps_to_problem_details_with_code_params_and_localized_detail()
    {
        AuthenticateAsUserA();
        var tooMany = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();

        var response = await Client.PostAsJsonAsync("/api/parts/batch", new BatchPartIdsRequestDto { PartIds = tooMany });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await ReadBodyAsync(response));
        var root = doc.RootElement;
        Assert.Equal(ErrorCodes.PartsBatchLimitExceeded, root.GetProperty("code").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal(200, root.GetProperty("params").GetProperty("max").GetInt32());
        Assert.Contains("200", root.GetProperty("detail").GetString());
    }

    // B-07 [P1] Invalid model → ValidationProblemDetails, code = COMMON_VALIDATION, per-field errors preserved.
    [Fact]
    public async Task Model_validation_failure_maps_to_validation_problem_details()
    {
        // /api/auth/login is [AllowAnonymous]; an empty/invalid body fails model validation.
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await ReadBodyAsync(response));
        var root = doc.RootElement;
        Assert.Equal(ErrorCodes.CommonValidation, root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any(), "Per-field validation errors should be preserved.");
    }

    // B-08 [P1] Each wired starter code triggered → mapped Problem Details returned.
    // (AUTH_* remain on the legacy AuthResponseDto contract during incremental migration — see B-09.)
    [Fact]
    public async Task Starter_codes_return_their_problem_details()
    {
        AuthenticateAsUserA();

        // COMMON_NOT_FOUND
        await AssertCodeAsync(
            HttpStatusCode.NotFound,
            ErrorCodes.CommonNotFound,
            () => Client.GetAsync($"/api/parts/{Guid.NewGuid()}"));

        // PARTS_BATCH_LIMIT_EXCEEDED
        await AssertCodeAsync(
            HttpStatusCode.BadRequest,
            ErrorCodes.PartsBatchLimitExceeded,
            () => Client.PostAsJsonAsync("/api/parts/batch", new BatchPartIdsRequestDto
            {
                PartIds = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList()
            }));

        // RIDES_ENDDATE_BEFORE_STARTDATE
        await AssertCodeAsync(
            HttpStatusCode.BadRequest,
            ErrorCodes.RidesEndDateBeforeStartDate,
            () => Client.PostAsJsonAsync("/api/rides/import/strava", new ImportStravaRidesRequestDto
            {
                StartDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }));

        // BIKES_DUPLICATE_STRAVA_ID
        await AssertCodeAsync(
            HttpStatusCode.BadRequest,
            ErrorCodes.BikesDuplicateStravaId,
            () => Client.PostAsJsonAsync("/api/bikes/sync", new SyncBikesRequestDto
            {
                Bikes = new List<SyncBikeDto>
                {
                    new() { Name = "A", StravaBikeId = "dup" },
                    new() { Name = "B", StravaBikeId = "dup" },
                }
            }));
    }

    // B-09 [P1] A migrated controller emits the new Problem Details shape while an un-migrated one
    //           still returns its legacy body (incremental migration coexists).
    [Fact]
    public async Task Migrated_and_legacy_controllers_coexist_during_incremental_migration()
    {
        AuthenticateAsUserA();

        // Migrated: Parts batch → Problem Details with a machine code.
        var migrated = await Client.PostAsJsonAsync("/api/parts/batch", new BatchPartIdsRequestDto
        {
            PartIds = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList()
        });
        using var migratedDoc = JsonDocument.Parse(await ReadBodyAsync(migrated));
        Assert.True(migratedDoc.RootElement.TryGetProperty("code", out _));

        // Legacy: Auth login with bad credentials → AuthResponseDto (message/success, no code).
        ClearAuth();
        var legacy = await Client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "nobody@example.com",
            Password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, legacy.StatusCode);
        using var legacyDoc = JsonDocument.Parse(await ReadBodyAsync(legacy));
        Assert.False(legacyDoc.RootElement.TryGetProperty("code", out _));
        Assert.True(legacyDoc.RootElement.TryGetProperty("message", out _));
    }

    // B-14 [P2] Persisted Language=de, but the response is localized from Accept-Language
    //           (no per-request DB read of the setting).
    [Fact]
    public async Task Response_culture_derives_from_accept_language_not_persisted_setting()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();
        var put = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto { Language = "de" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // Ask for Ukrainian; the German persisted setting must NOT influence the response.
        Client.DefaultRequestHeaders.Add("Accept-Language", "uk");
        var response = await Client.GetAsync($"/api/parts/{Guid.NewGuid()}");

        using var doc = JsonDocument.Parse(await ReadBodyAsync(response));
        var detail = doc.RootElement.GetProperty("detail").GetString();
        Assert.Equal("Запитуваний ресурс не знайдено.", detail);
        Assert.NotEqual("Die angeforderte Ressource wurde nicht gefunden.", detail);
    }

    // B-04 [P1] Deferred: no backend endpoint emits a rider-facing localized count yet. Count-bearing
    //           text is rendered frontend-side (ADR 0006 §E2, see Vitest F-13); revisit when ADR 0004
    //           out-of-app content lands.
    [Fact(Skip = "Deferred: no backend-generated count surface yet (ADR 0006 §E2 / ADR 0004).")]
    public void Backend_count_uses_ukrainian_plural_forms()
    {
    }

    // B-15 [P1] Deferred: needs an ILogger capture seam in the WAF host (escalated in the QA plan's
    //           Gaps as a shared ADR 0005 CI/tooling follow-up). The behaviour itself is implemented in
    //           LocalizedErrorFactory (non-prod warning on a code missing in every culture).
    [Fact(Skip = "Deferred: needs log-capture test infra (QA plan Gaps / ADR 0005 follow-up).")]
    public void Missing_code_logs_warning_in_non_production_and_shows_english()
    {
    }

    private async Task AssertCodeAsync(HttpStatusCode expectedStatus, string expectedCode, Func<Task<HttpResponseMessage>> act)
    {
        var response = await act();
        Assert.Equal(expectedStatus, response.StatusCode);
        using var doc = JsonDocument.Parse(await ReadBodyAsync(response));
        Assert.Equal(expectedCode, doc.RootElement.GetProperty("code").GetString());
    }
}
