using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    public class Ride
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Strava activity id when the ride was imported from Strava; null for manual rides. Entity key is <see cref="Id"/>.
        /// </summary>
        public long? StravaActivityId { get; set; }

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
        /// Raw distance in meters from an external source (e.g., Strava). Zero for manually created rides.
        /// </summary>
        public double RecordedDistance { get; set; }

        /// <summary>
        /// Business distance in meters used by application calculations. Set by the user or copied from Strava on first import.
        /// </summary>
        public double Distance { get; set; }

        public DateTime StartDateLocal { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
