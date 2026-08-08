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

    /// <summary>
    /// Request body for <c>POST /api/chaincycles/{id}/fill-empty-slots</c> (ADR 0010).
    /// </summary>
    public class FillEmptyChainCycleSlotsDto
    {
        /// <summary>
        /// 0-based index into <c>Chains</c> for which newly filled slot becomes active.
        /// Only meaningful when the cycle has no <c>ActiveChainId</c>.
        /// Null / omitted = None yet (leave active null). Ignored when the cycle already has an active chain.
        /// </summary>
        public int? ActiveNewSlotIndex { get; set; }

        /// <summary>
        /// UTC install time when this call sets a new active chain. Default = now. Ignored when not activating.
        /// </summary>
        public DateTime? InstallationDate { get; set; }
    }

    /// <summary>
    /// Mutation envelope for fill-empty-slots (ADR 0010).
    /// </summary>
    public class FillEmptyChainCycleSlotsResponseDto
    {
        public ChainCycleResponseDto ChainCycle { get; set; } = null!;
        public List<BikePartDto> CreatedParts { get; set; } = new();
        public List<Guid> AffectedPartIds { get; set; } = new();
    }
}
