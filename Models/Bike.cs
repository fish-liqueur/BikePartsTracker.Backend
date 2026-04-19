using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    public class Bike
    {
        [Key]
        public Guid Id { get; set; }

        public string? StravaBikeId { get; set; }

        [ForeignKey(nameof(User))]
        public Guid UserId { get; set; }
        public required User User { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public double TotalDistance { get; set; } // in km

        /// <summary>
        /// Distance from Strava in meters
        /// </summary>
        public double StravaDistance { get; set; }

        /// <summary>
        /// Whether the bike is active (shown by default)
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<BikePart> Parts { get; set; } = new List<BikePart>();
    }
}
