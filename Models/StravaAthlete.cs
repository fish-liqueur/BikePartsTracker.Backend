using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    /// <summary>
    /// Stores Strava-specific athlete/profile information
    /// </summary>
    public class StravaAthlete
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Foreign key to ExternalServiceIntegration
        /// </summary>
        [ForeignKey(nameof(Integration))]
        public Guid IntegrationId { get; set; }
        public required ExternalServiceIntegration Integration { get; set; }

        /// <summary>
        /// Strava athlete ID
        /// </summary>
        public long StravaId { get; set; }

        /// <summary>
        /// Athlete username
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Athlete first name
        /// </summary>
        public string? Firstname { get; set; }

        /// <summary>
        /// Athlete last name
        /// </summary>
        public string? Lastname { get; set; }

        /// <summary>
        /// Athlete city
        /// </summary>
        public string? City { get; set; }

        /// <summary>
        /// Athlete state
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Athlete country
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// When the athlete data was last synced from Strava
        /// </summary>
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
    }
}

