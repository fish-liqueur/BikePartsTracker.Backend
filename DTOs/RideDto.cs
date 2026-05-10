using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    public class RideDto
    {
        public Guid Id { get; set; }
        public long? StravaActivityId { get; set; }
        public Guid? BikeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? GearId { get; set; }
        public double Distance { get; set; }
        public double RecordedDistance { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartDateLocal { get; set; }
    }

    public class ImportStravaRidesRequestDto
    {
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }

    public class ImportStravaRidesResponseDto
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public List<RideDto> Rides { get; set; } = new();
        public RideMutationResultDto Affected { get; set; } = new();
    }

    /// <summary>
    /// Identifies entities affected by a ride mutation. The frontend uses these ids to mark cached
    /// data as dirty and refetch on demand.
    /// </summary>
    public class RideMutationResultDto
    {
        public List<Guid> AffectedRideIds { get; set; } = new();
        public List<Guid> AffectedPartIds { get; set; } = new();
        public List<Guid> AffectedWorkIds { get; set; } = new();
        public List<Guid> AffectedBikeIds { get; set; } = new();
    }

    /// <summary>
    /// Wrapper used by ride create/update responses so the frontend can update both the ride
    /// itself and the dirty-set of related entities in a single round trip.
    /// </summary>
    public class RideMutationResponseDto
    {
        public RideDto? Ride { get; set; }
        public RideMutationResultDto Affected { get; set; } = new();
    }

    public class CreateRideDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Type { get; set; }

        public string? GearId { get; set; }

        public Guid? BikeId { get; set; }

        /// <summary>Distance in meters used by business calculations.</summary>
        [Range(0, double.MaxValue)]
        public double Distance { get; set; }

        [Required]
        public DateTime StartDateLocal { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateRideDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? GearId { get; set; }
        public Guid? BikeId { get; set; }

        [Range(0, double.MaxValue)]
        public double? Distance { get; set; }

        public DateTime? StartDateLocal { get; set; }
        public bool? IsActive { get; set; }
    }
}
