using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    public class ChainCycleResponseDto
    {
        public Guid Id { get; set; }
        public Guid BikeId { get; set; }
        /// <summary>
        /// Ordered list of chain part IDs. Null entries = empty slots.
        /// Array length = cycle size.
        /// </summary>
        public List<Guid?> Chains { get; set; } = new();
        public Guid? ActiveChainId { get; set; }
        /// <summary>Rotation interval in metres.</summary>
        public double? IntervalMetres { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateChainCycleDto
    {
        [Required]
        public Guid BikeId { get; set; }

        /// <summary>
        /// Initial chains array. e.g. [null, null, null] for a 3-slot cycle,
        /// or [partId, null] for a 2-slot cycle with one chain assigned.
        /// </summary>
        public List<Guid?>? Chains { get; set; }

        public Guid? ActiveChainId { get; set; }
        /// <summary>Rotation interval in metres.</summary>
        public double? IntervalMetres { get; set; }
    }

    public class UpdateChainCycleDto
    {
        public List<Guid?>? Chains { get; set; }
        public Guid? ActiveChainId { get; set; }
        /// <summary>Rotation interval in metres.</summary>
        public double? IntervalMetres { get; set; }
    }
}
