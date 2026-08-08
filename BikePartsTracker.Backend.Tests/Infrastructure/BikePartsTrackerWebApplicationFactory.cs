using BikePartsTracker.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BikePartsTracker.Backend.Tests.Infrastructure;

public sealed class BikePartsTrackerWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string WebhookVerifyToken = "test-webhook-verify-token";

    private readonly string _connectionString;
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public FakeStravaService FakeStrava { get; } = new();
    public ThrowAfterNPartsFaultInjector FillFaultInjector { get; } = new();

    public BikePartsTrackerWebApplicationFactory(
        string connectionString,
        string jwtKey,
        string jwtIssuer,
        string jwtAudience)
    {
        _connectionString = connectionString;
        _jwtKey = jwtKey;
        _jwtIssuer = jwtIssuer;
        _jwtAudience = jwtAudience;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Jwt:Key"] = _jwtKey,
                ["Jwt:Issuer"] = _jwtIssuer,
                ["Jwt:Audience"] = _jwtAudience,
                ["Strava:WebhookVerifyToken"] = WebhookVerifyToken,
                ["Strava:ClientId"] = "test-client-id",
                ["Strava:ClientSecret"] = "test-client-secret",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStravaService>();
            services.AddSingleton<IStravaService>(FakeStrava);
            services.RemoveAll<IFillEmptySlotsFaultInjector>();
            services.AddSingleton<IFillEmptySlotsFaultInjector>(FillFaultInjector);
        });
    }
}
