using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// DTO for updating a part - allows null to explicitly clear optional fields
    /// </summary>
    public class UpdatePartDto
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public PartType? PartType { get; set; }

        public string? Brand { get; set; }

        public string? Model { get; set; }

        public DateTime? InstallationDate { get; set; }

        public double? MileageAtInstallation { get; set; }

        public Guid? BikeId { get; set; }

        public bool? IsActive { get; set; }

        public PartScheduleType? ScheduleType { get; set; }

        public double? ScheduleValue { get; set; }
    }
}
