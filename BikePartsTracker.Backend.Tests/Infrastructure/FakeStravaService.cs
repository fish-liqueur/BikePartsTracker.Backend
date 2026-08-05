using BikePartsTracker.DTOs;
using BikePartsTracker.Services;

namespace BikePartsTracker.Backend.Tests.Infrastructure;

/// <summary>
/// In-memory Strava stub for webhook / import integration tests.
/// </summary>
public sealed class FakeStravaService : IStravaService
{
    public List<StravaActivityDto> Activities { get; } = new();
    public Dictionary<long, StravaActivityDto> ActivitiesById { get; } = new();
    public int GetActivityCallCount { get; private set; }
    public int GetActivitiesCallCount { get; private set; }
    public bool ThrowOnGetActivity { get; set; }

    public Task<StravaTokenResponse?> ExchangeCodeForTokenAsync(string code, string redirectUri) =>
        Task.FromResult<StravaTokenResponse?>(null);

    public Task<bool> RevokeTokenAsync(string accessToken) => Task.FromResult(true);

    public Task<StravaTokenResponse?> RefreshTokenAsync(string refreshToken) =>
        Task.FromResult<StravaTokenResponse?>(null);

    public Task<StravaAthleteDto?> GetAthleteAsync(string accessToken) =>
        Task.FromResult<StravaAthleteDto?>(null);

    public Task<List<StravaActivityDto>> GetActivitiesAsync(
        string accessToken,
        long? before = null,
        long? after = null,
        int page = 1,
        int perPage = 30)
    {
        GetActivitiesCallCount++;
        var list = Activities
            .Where(a =>
            {
                var unix = new DateTimeOffset(a.StartDateLocal).ToUnixTimeSeconds();
                if (after.HasValue && unix <= after.Value) return false;
                if (before.HasValue && unix >= before.Value) return false;
                return true;
            })
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<StravaActivityDto?> GetActivityAsync(string accessToken, long activityId)
    {
        GetActivityCallCount++;
        if (ThrowOnGetActivity)
        {
            throw new HttpRequestException("stub failure");
        }

        ActivitiesById.TryGetValue(activityId, out var activity);
        return Task.FromResult(activity);
    }

    public void Reset()
    {
        Activities.Clear();
        ActivitiesById.Clear();
        GetActivityCallCount = 0;
        GetActivitiesCallCount = 0;
        ThrowOnGetActivity = false;
    }
}
