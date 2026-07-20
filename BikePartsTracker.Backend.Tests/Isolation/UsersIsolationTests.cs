using System.Net;
using System.Net.Http.Json;
using BikePartsTracker.Backend.Tests.Infrastructure;
using BikePartsTracker.DTOs;

namespace BikePartsTracker.Backend.Tests.Isolation;

public class UsersIsolationTests : IntegrationTestBase
{
    public UsersIsolationTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/users/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("POST", "/api/users")]
    [InlineData("PUT", "/api/users/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("DELETE", "/api/users/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    public async Task Removed_users_crud_does_not_return_user_data(string method, string path)
    {
        await SeedTwoUsersWithBikesAsync();
        ClearAuth();

        var response = method switch
        {
            "GET" => await Client.GetAsync(path),
            "POST" => await Client.PostAsJsonAsync(path, new
            {
                name = "Evil",
                email = "evil@example.com",
                passwordHash = "x"
            }),
            "PUT" => await Client.PutAsJsonAsync(path, new
            {
                id = UserAId,
                name = "Evil",
                email = "evil@example.com",
                passwordHash = "x"
            }),
            "DELETE" => await Client.DeleteAsync(path),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);

        var body = await ReadBodyAsync(response);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a@example.com", body);
        Assert.DoesNotContain("hash-a", body);
    }

    [Fact]
    public async Task Settings_require_auth()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/users/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Settings_work_for_authenticated_user()
    {
        await SeedTwoUsersWithBikesAsync();
        AuthenticateAsUserA();

        var get = await Client.GetAsync("/api/users/settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var settings = await ReadJsonAsync<UserSettingsDto>(get);
        Assert.NotNull(settings);

        var put = await Client.PutAsJsonAsync("/api/users/settings", new UpdateUserSettingsDto
        {
            showTips = false
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await ReadJsonAsync<UserSettingsDto>(put);
        Assert.NotNull(updated);
        Assert.False(updated.showTips);
    }

    [Fact]
    public async Task Auth_login_and_register_remain_anonymous()
    {
        ClearAuth();

        var register = await Client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            Name = "New Rider",
            Email = "new@example.com",
            Password = "secret1",
            ConfirmPassword = "secret1"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await Client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "new@example.com",
            Password = "secret1"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await ReadJsonAsync<AuthResponseDto>(login);
        Assert.NotNull(auth);
        Assert.True(auth.Success);
        Assert.False(string.IsNullOrEmpty(auth.Token));
    }
}
