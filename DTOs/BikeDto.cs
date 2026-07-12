using System.ComponentModel.DataAnnotations;
using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    public class BikeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BikeType Type { get; set; }
        public List<BikePartDto> Parts { get; set; } = new List<BikePartDto>();
        public double TotalDistance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? StravaId { get; set; }
        public double? StravaDistance { get; set; }
        public bool? IsActive { get; set; }
    }

    public class BikePartDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PartType PartType { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public DateTime? InstallationDate { get; set; }
        public double? MileageAtInstallation { get; set; }
        public Guid? BikeId { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Cumulative distance ridden on this part (sum of non-shadow usage period distances).
        /// </summary>
        public double TotalDistance { get; set; }

        /// <summary>
        /// Number of maintenance tasks for this part whose consumed value has reached or exceeded the trigger.
        /// </summary>
        public int PendingMaintenanceTasksCount { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Request body shared by the parts batch lookup endpoints. Carries the set of part ids the
    /// frontend wants to flush from its dirty-set.
    /// </summary>
    public class BatchPartIdsRequestDto
    {
        [Required]
        [MinLength(1)]
        public List<Guid> PartIds { get; set; } = new();
    }
}
