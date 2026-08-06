using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BikePartsTracker.Models
{
    public class ChainCycle
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(Bike))]
        public Guid BikeId { get; set; }

        [JsonIgnore]
        public Bike Bike { get; set; } = null!;

        /// <summary>
        /// Ordered list of chain part IDs forming the rotation.
        /// Null entries represent empty slots. Array length is the cycle size.
        /// Serialized to/from ChainsJson for persistence.
        /// </summary>
        [NotMapped]
        public List<Guid?> Chains
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ChainsJson))
                    return new List<Guid?>();
                try
                {
                    return JsonSerializer.Deserialize<List<Guid?>>(ChainsJson) ?? new List<Guid?>();
                }
                catch
                {
                    return new List<Guid?>();
                }
            }
            set
            {
                ChainsJson = value != null
                    ? JsonSerializer.Serialize(value)
                    : "[]";
            }
        }

        [Column("ChainsJson")]
        [JsonIgnore]
        public string ChainsJson { get; set; } = "[]";

        /// <summary>
        /// ID of the chain part currently installed on the bike from this cycle.
        /// </summary>
        public Guid? ActiveChainId { get; set; }

        /// <summary>
        /// Distance in metres between chain swaps within this cycle (ADR 0002).
        /// </summary>
        public double? IntervalMetres { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
