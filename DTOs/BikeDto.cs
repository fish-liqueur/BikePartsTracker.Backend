using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    public class BikeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BikeType Type { get; set; }
        public List<BikePartDto> Parts { get; set; } = new List<BikePartDto>();
        public double TotalDistance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? StravaId { get; set; }
        public double? StravaDistance { get; set; }
        public bool? IsActive { get; set; }
        public List<ChainCycleDto> ChainCycles { get; set; } = new List<ChainCycleDto>();
    }

    public class ChainCycleDto
    {
        public Guid Id { get; set; }
        /// <summary>
        /// Ordered list of chain part IDs forming the rotation.
        /// </summary>
        public List<Guid> Chains { get; set; } = new List<Guid>();
        /// <summary>
        /// ID of the chain part currently installed.
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

    public class BikePartDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PartType PartType { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public DateTime? InstallationDate { get; set; }
        public double? MileageAtInstallation { get; set; }
        public Guid? BikeId { get; set; }
        public List<PartUsageHistoryDto>? UsageHistory { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class PartUsageHistoryDto
    {
        public Guid Id { get; set; }
        public Guid PartId { get; set; }
        public double Mileage { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
