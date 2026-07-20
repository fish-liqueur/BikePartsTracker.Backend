using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BikePartsTracker.Backend.Tests.Infrastructure;

public sealed class BikePartsTrackerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

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
            });
        });
    }
}
