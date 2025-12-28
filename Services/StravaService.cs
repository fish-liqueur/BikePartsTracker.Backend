using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using BikePartsTracker.DTOs;

namespace BikePartsTracker.Services
{
    public interface IStravaService
    {
        Task<StravaTokenResponse?> ExchangeCodeForTokenAsync(string code, string redirectUri);
        Task<bool> RevokeTokenAsync(string accessToken);
        Task<StravaTokenResponse?> RefreshTokenAsync(string refreshToken);
        Task<StravaAthleteDto?> GetAthleteAsync(string accessToken);
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

        public async Task<StravaTokenResponse?> RefreshTokenAsync(string refreshToken)
        {
            var clientId = _configuration["Strava:ClientId"];
            var clientSecret = _configuration["Strava:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Strava ClientId and ClientSecret must be configured in appsettings.json");
            }

            var requestParams = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("client_id", clientId!.Trim()),
                new KeyValuePair<string, string>("client_secret", clientSecret!.Trim()),
                new KeyValuePair<string, string>("refresh_token", refreshToken.Trim()),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
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
                throw new InvalidOperationException($"Failed to refresh Strava token: {ex.Message}", ex);
            }
        }

        public async Task<StravaAthleteDto?> GetAthleteAsync(string accessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.strava.com/api/v3/athlete");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Strava API error: {response.StatusCode} - {errorContent}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var athleteResponse = JsonSerializer.Deserialize<StravaAthleteApiResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (athleteResponse == null)
                {
                    return null;
                }

                // Map to DTO
                var athleteDto = new StravaAthleteDto
                {
                    Id = athleteResponse.Id,
                    Username = athleteResponse.Username,
                    Firstname = athleteResponse.Firstname,
                    Lastname = athleteResponse.Lastname,
                    City = athleteResponse.City,
                    State = athleteResponse.State,
                    Country = athleteResponse.Country,
                    Bikes = athleteResponse.Bikes?.Select(b => new StravaBikeDto
                    {
                        Id = b.Id,
                        Name = b.Name,
                        Primary = b.Primary,
                        Distance = b.Distance
                    }).ToList() ?? new List<StravaBikeDto>()
                };

                return athleteDto;
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get Strava athlete: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Internal class to deserialize Strava token response
    /// </summary>
    public class StravaTokenResponse
    {
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("athlete")]
        public StravaAthleteDto? Athlete { get; set; }
    }

    /// <summary>
    /// Internal class to deserialize Strava API athlete response
    /// </summary>
    internal class StravaAthleteApiResponse
    {
        public long Id { get; set; }
        public string? Username { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public List<StravaBikeApiResponse>? Bikes { get; set; }
    }

    /// <summary>
    /// Internal class to deserialize Strava API bike response
    /// </summary>
    internal class StravaBikeApiResponse
    {
        public string Id { get; set; } = string.Empty;
        public bool Primary { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Distance { get; set; }
    }
}

