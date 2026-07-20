using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BikePartsTracker.Backend.Tests.Isolation;

public class BikesIsolationTests : IntegrationTestBase
{
    public BikesIsolationTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData("/api/bikes")]
    [InlineData("/api/bikes/11111111-1111-1111-1111-111111111111")]
    public async Task Anonymous_bikes_reads_return_401(string path)
    {
        await SeedTwoUsersWithBikesAsync();
        ClearAuth();

        var response = await Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_bikes_delete_returns_401()
    {
        await SeedTwoUsersWithBikesAsync();
        ClearAuth();

        var response = await Client.DeleteAsync($"/api/bikes/{BikeAId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Bikes.AnyAsync(b => b.Id == BikeAId));
    }

    [Fact]
    public async Task Authenticated_list_returns_only_caller_bikes()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var response = await Client.GetAsync("/api/bikes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bikes = await ReadJsonAsync<List<BikeDto>>(response);
        Assert.NotNull(bikes);
        Assert.Single(bikes);
        Assert.Equal(BikeAId, bikes[0].Id);
        Assert.Equal("Bike A", bikes[0].Name);

        var body = await ReadBodyAsync(response);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", body);
    }

    [Fact]
    public async Task Get_other_users_bike_returns_404()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var response = await Client.GetAsync($"/api/bikes/{BikeBId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadBodyAsync(response);
        Assert.DoesNotContain("Bike B", body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_other_users_bike_returns_404_and_does_not_delete()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var response = await Client.DeleteAsync($"/api/bikes/{BikeBId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Bikes.AnyAsync(b => b.Id == BikeBId));
    }

    [Fact]
    public async Task Owner_can_get_and_delete_own_bike()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var get = await Client.GetAsync($"/api/bikes/{BikeAId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var bike = await ReadJsonAsync<BikeDto>(get);
        Assert.NotNull(bike);
        Assert.Equal(BikeAId, bike.Id);

        var delete = await Client.DeleteAsync($"/api/bikes/{BikeAId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Bikes.AnyAsync(b => b.Id == BikeAId));
        Assert.True(await db.Bikes.AnyAsync(b => b.Id == BikeBId));
    }

    [Fact]
    public async Task Put_other_users_bike_returns_403()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var response = await Client.PutAsJsonAsync($"/api/bikes/{BikeBId}", new UpdateBikeDto
        {
            Name = "Hijacked"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Bike_responses_do_not_include_user_or_password_hash()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var response = await Client.GetAsync($"/api/bikes/{BikeAId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await ReadBodyAsync(response));
        Assert.False(doc.RootElement.TryGetProperty("user", out _));
        Assert.False(doc.RootElement.TryGetProperty("User", out _));
        Assert.False(doc.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(doc.RootElement.TryGetProperty("PasswordHash", out _));
    }
}
