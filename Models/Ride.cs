using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    public class Ride
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Strava activity identifier.
        /// </summary>
        public long StravaActivityId { get; set; }

        [ForeignKey(nameof(User))]
        public Guid UserId { get; set; }
        public required User User { get; set; }

        [ForeignKey(nameof(Bike))]
        public Guid? BikeId { get; set; }
        public Bike? Bike { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? GearId { get; set; }

        /// <summary>
        /// Distance from Strava in meters.
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// User-adjusted distance in meters.
        /// </summary>
        public double UserDistance { get; set; }

        public DateTime StartDateLocal { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
