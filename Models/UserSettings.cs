using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BikePartsTracker.Models
{
    public class UserSettings
    {
        [Key]
        [ForeignKey(nameof(User))]
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        /// <summary>
        /// Default chain cycle length (in number of cycles)
        /// </summary>
        public int DefaultChainCycleLength { get; set; } = 700;

        /// <summary>
        /// Default chain cycle interval in kilometers
        /// </summary>
        public int DefaultChainCycleIntervalKm { get; set; } = 3;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

