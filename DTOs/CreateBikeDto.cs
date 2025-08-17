using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    public class CreateBikeDto
    {
        [Required]
        public string StravaBikeId { get; set; } = string.Empty;

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public double TotalDistance { get; set; } = 0.0;
    }
}
