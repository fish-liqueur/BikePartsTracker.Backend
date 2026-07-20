using Testcontainers.PostgreSql;

namespace BikePartsTracker.Backend.Tests.Infrastructure;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    public const string JwtKey = "integration-test-jwt-key-at-least-32-characters-long";
    public const string JwtIssuer = "BikePartsTracker.Tests";
    public const string JwtAudience = "BikePartsTracker.Tests";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("BikePartsTrackerTests")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .Build();

    public BikePartsTrackerWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new BikePartsTrackerWebApplicationFactory(
            _postgres.GetConnectionString(),
            JwtKey,
            JwtIssuer,
            JwtAudience);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}
