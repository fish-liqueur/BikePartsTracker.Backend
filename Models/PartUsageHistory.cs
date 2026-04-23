using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    public class PartUsageHistory
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(BikePart))]
        public Guid BikePartId { get; set; }
        public required BikePart BikePart { get; set; }

        [ForeignKey(nameof(Bike))]
        public Guid? BikeId { get; set; }
        public Bike? Bike { get; set; }

        [ForeignKey(nameof(SourceUsagePeriod))]
        public Guid? SourceUsagePeriodId { get; set; }
        public PartUsageHistory? SourceUsagePeriod { get; set; }
        public ICollection<PartUsageHistory> ShadowChildren { get; set; } = new List<PartUsageHistory>();

        [ForeignKey(nameof(Work))]
        public Guid? WorkId { get; set; }
        public Work? Work { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Cached interval distance in meters.
        /// </summary>
        public double Distance { get; set; }

        public bool IsShadow { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}