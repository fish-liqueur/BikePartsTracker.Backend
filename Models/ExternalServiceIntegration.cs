using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    /// <summary>
    /// Represents an integration with an external service (Strava, Komoot, etc.)
    /// </summary>
    public class ExternalServiceIntegration
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(User))]
        public Guid UserId { get; set; }
        public required User User { get; set; }

        /// <summary>
        /// Type of external service (Strava, Komoot, etc.)
        /// </summary>
        public ExternalServiceType ServiceType { get; set; }

        /// <summary>
        /// Service-specific user ID (e.g., Strava athlete ID)
        /// </summary>
        public string ServiceUserId { get; set; } = string.Empty;

        /// <summary>
        /// OAuth access token
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// OAuth refresh token
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Token expiration time
        /// </summary>
        public DateTime TokenExpiry { get; set; }

        /// <summary>
        /// When the integration was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the integration was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Start of the contiguous UTC date range successfully covered by automatic import (gap-fill / webhooks).
        /// Internal watermark — not exposed on rider-facing DTOs.
        /// </summary>
        public DateTime? AutoImportCoveredFrom { get; set; }

        /// <summary>
        /// End of the contiguous UTC date range successfully covered by automatic import.
        /// Internal watermark — not exposed on rider-facing DTOs.
        /// </summary>
        public DateTime? AutoImportCoveredTo { get; set; }

        /// <summary>
        /// Navigation property to service-specific athlete/profile data
        /// </summary>
        public StravaAthlete? StravaAthlete { get; set; }
    }
}

