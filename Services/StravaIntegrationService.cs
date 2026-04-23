using BikePartsTracker.Data;
using BikePartsTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Services
{
    public interface IStravaIntegrationService
    {
        Task<ExternalServiceIntegration?> GetUserStravaIntegrationAsync(Guid userId);
        Task<string?> EnsureValidAccessTokenAsync(ExternalServiceIntegration integration);
    }

    public class StravaIntegrationService : IStravaIntegrationService
    {
        private readonly AppDbContext _context;
        private readonly IStravaService _stravaService;

        public StravaIntegrationService(AppDbContext context, IStravaService stravaService)
        {
            _context = context;
            _stravaService = stravaService;
        }

        public Task<ExternalServiceIntegration?> GetUserStravaIntegrationAsync(Guid userId)
        {
            return _context.ExternalServiceIntegrations
                .Include(i => i.StravaAthlete)
                .FirstOrDefaultAsync(i => i.UserId == userId && i.ServiceType == ExternalServiceType.Strava);
        }

        public async Task<string?> EnsureValidAccessTokenAsync(ExternalServiceIntegration integration)
        {
            if (string.IsNullOrEmpty(integration.AccessToken))
            {
                return null;
            }

            var tokenExpiryTime = integration.TokenExpiry;
            var bufferTime = TimeSpan.FromMinutes(5);
            if (DateTime.UtcNow.Add(bufferTime) < tokenExpiryTime)
            {
                return integration.AccessToken;
            }

            if (string.IsNullOrEmpty(integration.RefreshToken))
            {
                return null;
            }

            var tokenResponse = await _stravaService.RefreshTokenAsync(integration.RefreshToken);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                return null;
            }

            integration.AccessToken = tokenResponse.AccessToken;
            integration.RefreshToken = tokenResponse.RefreshToken;
            integration.TokenExpiry = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.ExpiresAt).UtcDateTime;
            integration.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return integration.AccessToken;
        }
    }
}
