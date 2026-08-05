using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http;
using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Services;
using BikePartsTracker.Models;

namespace BikePartsTracker.Controllers
{
    /// <summary>
    /// Controller for handling Strava OAuth integration
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StravaController : ControllerBase
    {
        private readonly IStravaService _stravaService;
        private readonly IStravaIntegrationService _stravaIntegrationService;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public StravaController(
            IStravaService stravaService,
            IStravaIntegrationService stravaIntegrationService,
            AppDbContext context,
            IConfiguration configuration)
        {
            _stravaService = stravaService;
            _stravaIntegrationService = stravaIntegrationService;
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// Exchange Strava authorization code for access token and store it for the authenticated user
        /// </summary>
        /// <param name="request">Strava authorization request with code and redirect URI</param>
        /// <returns>Strava authorization response with athlete information</returns>
        /// <response code="200">Strava connected successfully</response>
        /// <response code="400">Invalid request data</response>
        /// <response code="401">User not authenticated</response>
        /// <response code="500">Error connecting to Strava</response>
        [HttpPost("authorize")]
        [ProducesResponseType(typeof(StravaAuthResponseDto), 200)]
        [ProducesResponseType(typeof(StravaAuthResponseDto), 400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<StravaAuthResponseDto>> Authorize([FromBody] StravaAuthorizeRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new StravaAuthResponseDto
                {
                    Success = false,
                    Message = "Invalid request data"
                });
            }

            try
            {
                // Get current user from JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized();
                }

                // Exchange code for token with Strava
                StravaTokenResponse? tokenResponse;
                try
                {
                    tokenResponse = await _stravaService.ExchangeCodeForTokenAsync(request.Code, request.RedirectUri);
                }
                catch (HttpRequestException httpEx)
                {
                    // Strava API returned an error - return the actual error message
                    return StatusCode(500, new StravaAuthResponseDto
                    {
                        Success = false,
                        Message = httpEx.Message
                    });
                }
                catch (InvalidOperationException opEx)
                {
                    // Configuration or operation error
                    return StatusCode(500, new StravaAuthResponseDto
                    {
                        Success = false,
                        Message = opEx.Message
                    });
                }
                
                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken) || string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    return StatusCode(500, new StravaAuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid token response from Strava: AccessToken or RefreshToken is missing"
                    });
                }

                // Get user from database
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Unauthorized();
                }

                var serviceUserId = tokenResponse.Athlete?.Id.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(serviceUserId))
                {
                    // One Strava athlete → one app user (webhook owner_id mapping; ADR 0003).
                    var athleteTaken = await _context.ExternalServiceIntegrations.AnyAsync(i =>
                        i.ServiceType == ExternalServiceType.Strava &&
                        i.ServiceUserId == serviceUserId &&
                        i.UserId != userId);
                    if (athleteTaken)
                    {
                        return BadRequest(new StravaAuthResponseDto
                        {
                            Success = false,
                            Message = "This Strava account is already connected to another BikePartsTracker user."
                        });
                    }
                }

                // Find existing Strava integration or create new one
                var integration = await _context.ExternalServiceIntegrations
                    .Include(i => i.StravaAthlete)
                    .FirstOrDefaultAsync(i => i.UserId == userId && i.ServiceType == ExternalServiceType.Strava);

                if (integration == null)
                {
                    // Create new integration
                    integration = new ExternalServiceIntegration
                    {
                        Id = Guid.NewGuid(),
                        User = user,
                        UserId = userId,
                        ServiceType = ExternalServiceType.Strava,
                        ServiceUserId = serviceUserId,
                        AccessToken = tokenResponse.AccessToken,
                        RefreshToken = tokenResponse.RefreshToken,
                        TokenExpiry = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.ExpiresAt).UtcDateTime,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ExternalServiceIntegrations.Add(integration);
                }
                else
                {
                    // Update existing integration
                    integration.ServiceUserId = serviceUserId;
                    integration.AccessToken = tokenResponse.AccessToken;
                    integration.RefreshToken = tokenResponse.RefreshToken;
                    integration.TokenExpiry = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.ExpiresAt).UtcDateTime;
                    integration.UpdatedAt = DateTime.UtcNow;
                }

                // Store or update athlete data
                if (tokenResponse.Athlete != null)
                {
                    if (integration.StravaAthlete == null)
                    {
                        // Create new athlete record
                        var athlete = new StravaAthlete
                        {
                            Id = Guid.NewGuid(),
                            Integration = integration,
                            IntegrationId = integration.Id,
                            StravaId = tokenResponse.Athlete.Id,
                            Username = tokenResponse.Athlete.Username,
                            Firstname = tokenResponse.Athlete.Firstname,
                            Lastname = tokenResponse.Athlete.Lastname,
                            City = tokenResponse.Athlete.City,
                            State = tokenResponse.Athlete.State,
                            Country = tokenResponse.Athlete.Country,
                            LastSyncedAt = DateTime.UtcNow
                        };
                        integration.StravaAthlete = athlete;
                        _context.StravaAthletes.Add(athlete);
                    }
                    else
                    {
                        // Update existing athlete record
                        integration.StravaAthlete.StravaId = tokenResponse.Athlete.Id;
                        integration.StravaAthlete.Username = tokenResponse.Athlete.Username;
                        integration.StravaAthlete.Firstname = tokenResponse.Athlete.Firstname;
                        integration.StravaAthlete.Lastname = tokenResponse.Athlete.Lastname;
                        integration.StravaAthlete.City = tokenResponse.Athlete.City;
                        integration.StravaAthlete.State = tokenResponse.Athlete.State;
                        integration.StravaAthlete.Country = tokenResponse.Athlete.Country;
                        integration.StravaAthlete.LastSyncedAt = DateTime.UtcNow;
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException dbEx)
                {
                    var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                    return StatusCode(500, new StravaAuthResponseDto
                    {
                        Success = false,
                        Message = $"Database error: {innerMessage}"
                    });
                }

                // Map athlete information for response
                var athleteDto = integration.StravaAthlete != null ? new StravaAthleteDto
                {
                    Id = integration.StravaAthlete.StravaId,
                    Username = integration.StravaAthlete.Username,
                    Firstname = integration.StravaAthlete.Firstname,
                    Lastname = integration.StravaAthlete.Lastname,
                    City = integration.StravaAthlete.City,
                    State = integration.StravaAthlete.State,
                    Country = integration.StravaAthlete.Country
                } : null;

                return Ok(new StravaAuthResponseDto
                {
                    Success = true,
                    Message = "Strava connected successfully",
                    Athlete = athleteDto
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new StravaAuthResponseDto
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(500, new StravaAuthResponseDto
                {
                    Success = false,
                    Message = $"Error connecting to Strava: {ex.Message}"
                });
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                return StatusCode(500, new StravaAuthResponseDto
                {
                    Success = false,
                    Message = $"Database error: {innerMessage}"
                });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new StravaAuthResponseDto
                {
                    Success = false,
                    Message = $"An unexpected error occurred: {innerMessage}"
                });
            }
        }

        /// <summary>
        /// Get the Strava OAuth authorization URL with the correct scope
        /// </summary>
        /// <param name="redirectUri">The redirect URI to use after authorization</param>
        /// <returns>Authorization URL</returns>
        /// <response code="200">Returns the authorization URL</response>
        [HttpGet("authorize-url")]
        [ProducesResponseType(200)]
        public ActionResult GetAuthorizeUrl([FromQuery] string redirectUri)
        {
            var clientId = _configuration["Strava:ClientId"];
            if (string.IsNullOrEmpty(clientId))
            {
                return StatusCode(500, new { message = "Strava ClientId is not configured" });
            }

            var scope = "profile:read_all,activity:read_all";
            var authorizeUrl = $"https://www.strava.com/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString(scope)}";
            
            return Ok(new { authorizeUrl });
        }

        /// <summary>
        /// Disconnect Strava integration by revoking the token and removing it from the database
        /// </summary>
        /// <returns>Disconnect response indicating success</returns>
        /// <response code="200">Strava disconnected successfully</response>
        /// <response code="401">User not authenticated</response>
        [HttpPost("disconnect")]
        [ProducesResponseType(typeof(StravaDisconnectResponseDto), 200)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<StravaDisconnectResponseDto>> Disconnect()
        {
            try
            {
                // Get current user from JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized();
                }

                // Find Strava integration
                var integration = await _stravaIntegrationService.GetUserStravaIntegrationAsync(userId);

                if (integration != null)
                {
                    // Revoke the Strava token if it exists
                    if (!string.IsNullOrEmpty(integration.AccessToken))
                    {
                        await _stravaService.RevokeTokenAsync(integration.AccessToken);
                    }

                    // Remove the integration (cascade will remove athlete data)
                    _context.ExternalServiceIntegrations.Remove(integration);
                    await _context.SaveChangesAsync();
                }

                return Ok(new StravaDisconnectResponseDto
                {
                    Success = true
                });
            }
            catch (Exception)
            {
                // Even if revoke fails, we still remove the integration from our database
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                {
                    var integration = await _context.ExternalServiceIntegrations
                        .FirstOrDefaultAsync(i => i.UserId == userId && i.ServiceType == ExternalServiceType.Strava);
                    
                    if (integration != null)
                    {
                        _context.ExternalServiceIntegrations.Remove(integration);
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new StravaDisconnectResponseDto
                {
                    Success = true
                });
            }
        }

        /// <summary>
        /// Get Strava athlete data for the authenticated user (fetches fresh data from Strava API)
        /// </summary>
        /// <returns>Strava athlete information including bikes</returns>
        /// <response code="200">Returns athlete data</response>
        /// <response code="404">Strava integration not found</response>
        /// <response code="401">User not authenticated</response>
        [HttpGet("athlete")]
        [ProducesResponseType(typeof(StravaAthleteDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<StravaAthleteDto>> GetAthlete()
        {
            try
            {
                // Get current user from JWT token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized();
                }

                // Find Strava integration
                var integration = await _context.ExternalServiceIntegrations
                    .Include(i => i.StravaAthlete)
                    .FirstOrDefaultAsync(i => i.UserId == userId && i.ServiceType == ExternalServiceType.Strava);

                if (integration == null)
                {
                    return NotFound(new { message = "Strava integration not found" });
                }

                // Ensure token is valid (refresh if needed)
                var accessToken = await _stravaIntegrationService.EnsureValidAccessTokenAsync(integration);
                if (string.IsNullOrEmpty(accessToken))
                {
                    // Provide more detailed error information
                    var hasAccessToken = !string.IsNullOrEmpty(integration.AccessToken);
                    var hasRefreshToken = !string.IsNullOrEmpty(integration.RefreshToken);
                    var isExpired = DateTime.UtcNow >= integration.TokenExpiry;
                    
                    var errorDetails = new
                    {
                        message = "Failed to obtain valid access token",
                        details = new
                        {
                            hasAccessToken = hasAccessToken,
                            hasRefreshToken = hasRefreshToken,
                            isExpired = isExpired,
                            tokenExpiry = integration.TokenExpiry,
                            suggestion = !hasAccessToken 
                                ? "Access token is missing. Please reconnect Strava." 
                                : !hasRefreshToken 
                                    ? "Refresh token is missing. Please reconnect Strava."
                                    : isExpired 
                                        ? "Token expired and refresh failed. Please reconnect Strava."
                                        : "Unknown error. Please reconnect Strava."
                        }
                    };
                    
                    return StatusCode(500, errorDetails);
                }

                // Fetch fresh athlete data from Strava API
                var athleteDto = await _stravaService.GetAthleteAsync(accessToken);
                if (athleteDto == null)
                {
                    return NotFound(new { message = "Athlete data not found" });
                }

                return Ok(athleteDto);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(500, new { message = $"Error fetching from Strava API: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
            }
        }

    }
}

