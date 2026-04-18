using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    public class BikePart
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(User))]
        public Guid UserId { get; set; }
        public User? User { get; set; }

        [ForeignKey(nameof(Bike))]
        public Guid? BikeId { get; set; }
        public Bike? Bike { get; set; }

        public string Name { get; set; } = string.Empty; // e.g., "Chain #1"
        
        public string? Description { get; set; }
        
        /// <summary>
        /// Part type as enum (primary)
        /// </summary>
        public PartType PartType { get; set; }
        
        /// <summary>
        /// Part type as string (legacy/compatibility)
        /// </summary>
        public string Type { get; set; } = string.Empty; // e.g., "Chain", "Tyre"

        public string? Brand { get; set; }
        
        public string? Model { get; set; }
        
        public DateTime? InstallationDate { get; set; }
        
        public double? MileageAtInstallation { get; set; }

        public string HistoryJson { get; set; } = "[]"; // list of manipulations

        public PartScheduleType ScheduleType { get; set; }
        public double ScheduleValue { get; set; } // km or days

        /// <summary>
        /// Whether the part is active (e.g. still in use / shown by default).
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PartUsageHistory> UsageHistory { get; set; } = new List<PartUsageHistory>();
    }
}