using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// DTO for updating a bike. Omitted fields are not changed.
    /// </summary>
    public class UpdateBikeDto
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public BikeType? Type { get; set; }

        public double? TotalDistance { get; set; }

        public double? StravaDistance { get; set; }

        public string? StravaId { get; set; }

        public bool? IsActive { get; set; }
    }
}
