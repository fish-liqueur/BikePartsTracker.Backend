using System.Text.Json;
using System.Collections.Generic;
using BikePartsTracker.DTOs;

namespace BikePartsTracker.Services
{
    public interface IStravaService
    {
        Task<StravaTokenResponse?> ExchangeCodeForTokenAsync(string code, string redirectUri);
        Task<bool> RevokeTokenAsync(string accessToken);
    }

    public class StravaService : IStravaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public StravaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<StravaTokenResponse?> ExchangeCodeForTokenAsync(string code, string redirectUri)
        {
            var clientId = _configuration["Strava:ClientId"];
            var clientSecret = _configuration["Strava:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Strava ClientId and ClientSecret must be configured in appsettings.json");
            }

            // Build request parameters and trim whitespace (important for config values)
            var requestParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("client_id", clientId!.Trim()),
                new KeyValuePair<string, string>("client_secret", clientSecret!.Trim()),
                new KeyValuePair<string, string>("code", code.Trim()),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", (redirectUri ?? string.Empty).Trim())
            };

            var requestContent = new FormUrlEncodedContent(requestParams);

            try
            {
                var response = await _httpClient.PostAsync("https://www.strava.com/oauth/token", requestContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Strava API error: {response.StatusCode} - {errorContent}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<StravaTokenResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return tokenResponse;
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to exchange Strava code for token: {ex.Message}", ex);
            }
        }

        public async Task<bool> RevokeTokenAsync(string accessToken)
        {
            var clientId = _configuration["Strava:ClientId"];

            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("Strava ClientId must be configured in appsettings.json");
            }

            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", _configuration["Strava:ClientSecret"] ?? string.Empty),
                new KeyValuePair<string, string>("token", accessToken)
            });

            try
            {
                var response = await _httpClient.PostAsync("https://www.strava.com/oauth/deauthorize", requestContent);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Internal class to deserialize Strava token response
    /// </summary>
    public class StravaTokenResponse
    {
        public string TokenType { get; set; } = string.Empty;
        public long ExpiresAt { get; set; }
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public StravaAthleteDto? Athlete { get; set; }
    }
}

