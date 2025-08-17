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

        public string ActionType { get; set; } = string.Empty; // Installed, Removed, Maintained
        public DateTime Date { get; set; }

        public double OdometerAtAction { get; set; } // km at the time
        public string? Notes { get; set; }
    }
}