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

        public List<CreateChainCycleDto>? ChainCycles { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? StravaId { get; set; }

        public bool? IsActive { get; set; }
    }

    public class CreateChainCycleDto
    {
        /// <summary>
        /// Ordered list of chain part IDs forming the rotation. 
        /// </summary>
        public List<Guid?>? Chains { get; set; }

        /// <summary>
        /// ID of the chain part currently installed. Null means none active yet.
        /// </summary>
        public Guid? ActiveChainId { get; set; }

        /// <summary>
        /// Distance in km between chain swaps within this cycle.
        /// </summary>
        public double? IntervalKm { get; set; }

        /// <summary>
        /// Total number of chains in the rotation.
        /// </summary>
        public int? CycleLength { get; set; }
    }
}
