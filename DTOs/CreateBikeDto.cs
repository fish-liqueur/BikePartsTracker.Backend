using System.ComponentModel.DataAnnotations;
using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    public class CreateBikeDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public BikeType? Type { get; set; }

        public double? TotalDistance { get; set; }

        public double? StravaDistance { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? StravaId { get; set; }

        public bool? IsActive { get; set; }
    }
}
