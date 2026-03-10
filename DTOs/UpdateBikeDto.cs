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

        /// <summary>
        /// null or omitted = no change; empty array = remove all cycles; array = full replacement.
        /// </summary>
        public List<UpdateChainCycleDto>? ChainCycles { get; set; }

        public string? StravaId { get; set; }

        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Used within UpdateBikeDto to replace chain cycles.
    /// When an Id is provided it is reused so existing cycle IDs are preserved.
    /// Null values clear the corresponding field.
    /// </summary>
    public class UpdateChainCycleDto
    {
        /// <summary>
        /// Existing cycle ID to preserve. Omit to create a new cycle.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        /// Ordered list of chain part IDs. Null treated as empty list.
        /// </summary>
        public List<Guid>? Chains { get; set; }

        /// <summary>
        /// ID of currently installed chain. Null = no active chain (clear).
        /// </summary>
        public Guid? ActiveChainId { get; set; }

        /// <summary>
        /// Interval in km between swaps. Null = clear.
        /// </summary>
        public double? IntervalKm { get; set; }

        /// <summary>
        /// Number of chains in the rotation. Null = clear.
        /// </summary>
        public int? CycleLength { get; set; }
    }
}
