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
