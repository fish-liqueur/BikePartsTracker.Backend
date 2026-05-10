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
        /// Number of works for this part whose consumed value has reached or exceeded the trigger.
        /// </summary>
        public int PendingWorksCount { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Request body for the parts batch lookup endpoint. Used by the frontend to flush its
    /// dirty-set after ride mutations without making one request per part.
    /// </summary>
    public class BatchPartsRequestDto
    {
        [Required]
        [MinLength(1)]
        public List<Guid> PartIds { get; set; } = new();

        /// <summary>
        /// When true, each entry includes the non-shadow usage history for that part
        /// (sorted by <c>StartDate</c> ascending).
        /// </summary>
        public bool IncludeHistory { get; set; } = false;
    }

    /// <summary>
    /// One entry in the parts batch response. <see cref="History"/> is non-null only when the
    /// caller asked for histories; an empty list means the part has no records yet.
    /// </summary>
    public class BatchPartEntryDto
    {
        public BikePartDto Part { get; set; } = default!;
        public List<UsagePeriodDto>? History { get; set; }
    }
}
