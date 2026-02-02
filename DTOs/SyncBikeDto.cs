using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Data transfer object for syncing a bike
    /// </summary>
    public class SyncBikeDto
    {
        /// <summary>
        /// Internal bike ID (null for new bikes)
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Strava bike ID (null if not linked to Strava)
        /// </summary>
        public string? StravaBikeId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Type { get; set; } = string.Empty;

        /// <summary>
        /// Total distance in km (user's tracking)
        /// </summary>
        public double TotalDistance { get; set; }

        /// <summary>
        /// Distance from Strava in meters
        /// </summary>
        public double StravaDistance { get; set; }

        /// <summary>
        /// Whether the bike is active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}

