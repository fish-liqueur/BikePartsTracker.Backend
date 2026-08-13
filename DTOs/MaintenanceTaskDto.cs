using System.ComponentModel.DataAnnotations;
using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    public class MaintenanceTaskDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public MaintenanceTaskType Type { get; set; }
        public MaintenanceTaskTriggerType TriggerType { get; set; }
        public MaintenanceTaskParentType ParentType { get; set; }
        public Guid ParentId { get; set; }
        public double TriggerValue { get; set; }
        public bool IsActive { get; set; }

        public double ConsumedValue { get; set; }
        public double RemainingValue { get; set; }
        public bool NeedsAttention { get; set; }
    }

    public class CreateMaintenanceTaskDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public MaintenanceTaskType Type { get; set; } = MaintenanceTaskType.OneTime;
        public MaintenanceTaskTriggerType TriggerType { get; set; } = MaintenanceTaskTriggerType.Distance;
        public MaintenanceTaskParentType ParentType { get; set; } = MaintenanceTaskParentType.Part;
        public Guid ParentId { get; set; }
        public double TriggerValue { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateMaintenanceTaskDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public MaintenanceTaskType? Type { get; set; }
        public MaintenanceTaskTriggerType? TriggerType { get; set; }
        public MaintenanceTaskParentType? ParentType { get; set; }
        public Guid? ParentId { get; set; }
        public double? TriggerValue { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>ADR 0011 — acknowledge an occurrence ("I did it!" / "Do it now").</summary>
    public class AcknowledgeMaintenanceTaskDto
    {
        /// <summary>
        /// Required <c>true</c> when the task is not yet due (<c>consumed &lt; TriggerValue</c>).
        /// Optional when due (<see cref="MaintenanceTaskDto.NeedsAttention"/>).
        /// </summary>
        public bool Force { get; set; }
    }

    public class AcknowledgeMaintenanceTaskResponseDto
    {
        public required MaintenanceTaskDto MaintenanceTask { get; set; }
        public RideMutationResultDto Affected { get; set; } = new();
    }
}
