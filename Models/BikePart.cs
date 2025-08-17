using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    public class BikePart
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(Bike))]
        public Guid BikeId { get; set; }
        public required Bike Bike { get; set; }

        public string Name { get; set; } = string.Empty; // e.g., "Chain #1"
        public string Type { get; set; } = string.Empty; // e.g., "Chain", "Tyre"

        public string HistoryJson { get; set; } = "[]"; // list of manipulations

        public PartScheduleType ScheduleType { get; set; }
        public double ScheduleValue { get; set; } // km or days

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PartUsageHistory> UsageHistory { get; set; } = new List<PartUsageHistory>();
    }
}