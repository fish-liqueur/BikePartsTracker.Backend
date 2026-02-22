using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// DTO for updating a bike - allows null to explicitly clear optional fields
    /// </summary>
    public class UpdateBikeDto
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public BikeType? Type { get; set; }

        public double? TotalDistance { get; set; }

        public double? StravaDistance { get; set; }

        public int? ChainsCycleLength { get; set; }

        public int? ChainCycleInterval { get; set; }

        /// <summary>
        /// null = clear, undefined = no change, array = set value
        /// Accepts strings (will be converted to Guids) or Guids
        /// </summary>
        public List<Guid?>? ChainsInCycle { get; set; }

        /// <summary>
        /// null = clear, undefined = no change, string = set value
        /// Accepts string (will be converted to Guid) or Guid
        /// </summary>
        public Guid? ActiveChainId { get; set; }

        public string? StravaId { get; set; }

        public bool? IsActive { get; set; }
    }
}

