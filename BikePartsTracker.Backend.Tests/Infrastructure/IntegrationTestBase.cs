using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BikePartsTracker.BackgroundJobs;
using BikePartsTracker.Data;
using BikePartsTracker.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.Infrastructure;

[Collection(PostgresCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IntegrationTestFixture Fixture;
    protected HttpClient Client = null!;

    protected static readonly Guid UserAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid UserBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid BikeAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid BikeBId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected IntegrationTestBase(IntegrationTestFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await DatabaseReset.TruncateAsync(Fixture.Factory.Services);
        Fixture.Factory.FakeStrava.Reset();
        // Drain any leftover jobs from a prior test (queue is singleton).
        while (Fixture.Factory.Services.GetRequiredService<IBackgroundJobQueue>().TryDequeue(out _))
        {
        }
        Client = Fixture.Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected async Task SeedTwoUsersWithBikesAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userA = new User
        {
            Id = UserAId,
            Name = "User A",
            Email = "a@example.com",
            PasswordHash = "hash-a",
            CreatedAt = DateTime.UtcNow
        };
        var userB = new User
        {
            Id = UserBId,
            Name = "User B",
            Email = "b@example.com",
            PasswordHash = "hash-b",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(userA, userB);
        db.Bikes.AddRange(
            new Bike
            {
                Id = BikeAId,
                UserId = UserAId,
                User = userA,
                Name = "Bike A",
                Description = "Owned by A",
                Type = BikeType.Road.ToString(),
                TotalDistance = 10000,
                StravaDistance = 10000,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Bike
            {
                Id = BikeBId,
                UserId = UserBId,
                User = userB,
                Name = "Bike B",
                Description = "Owned by B",
                Type = BikeType.Gravel.ToString(),
                TotalDistance = 20000,
                StravaDistance = 20000,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }

    protected void AuthenticateAs(Guid userId, string email, string name)
    {
        var token = JwtTestHelper.Mint(
            userId,
            email,
            name,
            IntegrationTestFixture.JwtKey,
            IntegrationTestFixture.JwtIssuer,
            IntegrationTestFixture.JwtAudience);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected void AuthenticateAsUserA() => AuthenticateAs(UserAId, "a@example.com", "User A");

    protected void AuthenticateAsUserB() => AuthenticateAs(UserBId, "b@example.com", "User B");

    protected void ClearAuth() => Client.DefaultRequestHeaders.Authorization = null;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    protected static async Task<string> ReadBodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync();

    protected static Task<T?> ReadJsonAsync<T>(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(JsonOptions);
}
