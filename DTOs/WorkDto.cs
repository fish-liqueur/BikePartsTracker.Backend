using System.ComponentModel.DataAnnotations;
using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    public class WorkDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public WorkType Type { get; set; }
        public WorkTriggerType TriggerType { get; set; }
        public WorkParentType ParentType { get; set; }
        public Guid ParentId { get; set; }
        public double TriggerValue { get; set; }
        public bool IsActive { get; set; }

        public double ConsumedValue { get; set; }
        public double RemainingValue { get; set; }
        public bool NeedsAttention { get; set; }
    }

    public class CreateWorkDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public WorkType Type { get; set; } = WorkType.OneTime;
        public WorkTriggerType TriggerType { get; set; } = WorkTriggerType.Distance;
        public WorkParentType ParentType { get; set; } = WorkParentType.Part;
        public Guid ParentId { get; set; }
        public double TriggerValue { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateWorkDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public WorkType? Type { get; set; }
        public WorkTriggerType? TriggerType { get; set; }
        public WorkParentType? ParentType { get; set; }
        public Guid? ParentId { get; set; }
        public double? TriggerValue { get; set; }
        public bool? IsActive { get; set; }
    }
}
