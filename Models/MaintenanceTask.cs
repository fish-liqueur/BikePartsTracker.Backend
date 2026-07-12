using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    public class MaintenanceTask
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(User))]
        public Guid UserId { get; set; }
        public required User User { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }

        public MaintenanceTaskType Type { get; set; } = MaintenanceTaskType.OneTime;
        public MaintenanceTaskTriggerType TriggerType { get; set; } = MaintenanceTaskTriggerType.Distance;
        public MaintenanceTaskParentType ParentType { get; set; } = MaintenanceTaskParentType.Part;

        /// <summary>
        /// Parent entity identifier. Depends on ParentType.
        /// </summary>
        public Guid ParentId { get; set; }

        /// <summary>
        /// Trigger threshold value.
        /// Distance trigger -> meters, Time trigger -> days.
        /// </summary>
        public double TriggerValue { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PartUsageHistory> ShadowUsagePeriods { get; set; } = new List<PartUsageHistory>();
    }
}
