using System.ComponentModel.DataAnnotations;
using BikePartsTracker.Models;

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

        public BikeType? Type { get; set; }

        /// <summary>
        /// Rider-tracked total distance in metres (ADR 0002).
        /// </summary>
        public double TotalDistance { get; set; }

        /// <summary>
        /// Distance from Strava in metres.
        /// </summary>
        public double StravaDistance { get; set; }

        /// <summary>
        /// Whether the bike is active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}

