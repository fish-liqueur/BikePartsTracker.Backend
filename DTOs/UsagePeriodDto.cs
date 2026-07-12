using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    public class UsagePeriodDto
    {
        public Guid Id { get; set; }
        public Guid BikePartId { get; set; }
        public Guid? BikeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double Distance { get; set; }
        public bool IsShadow { get; set; }
        public Guid? MaintenanceTaskId { get; set; }
        public Guid? SourceUsagePeriodId { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateUsagePeriodDto
    {
        [Required]
        public Guid BikePartId { get; set; }

        public Guid? BikeId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateUsagePeriodDto
    {
        public Guid? BikeId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Notes { get; set; }
    }
}
