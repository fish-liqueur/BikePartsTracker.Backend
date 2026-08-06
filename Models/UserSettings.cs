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
        public int DefaultChainCycleLength { get; set; } = 3;

        /// <summary>
        /// Default chain cycle interval in metres (ADR 0002). Default 700 km → 700_000 m.
        /// </summary>
        public int DefaultChainCycleIntervalMetres { get; set; } = 700_000;

        /// <summary>
        /// Whether to use the chain cycle by default
        /// </summary>  
        public bool defaultUseChainCycle { get; set; } = true;

        /// <summary>
        /// Whether to show tips by default
        /// </summary>
        public bool showTips { get; set; } = true;

        /// <summary>
        /// The rider's preferred language as a BCP-47 tag (e.g. "en", "de", "ru", "uk").
        /// Null means "no explicit choice" and resolves to English at the display boundary.
        /// Source of truth for startup resolution and out-of-app content (ADR 0006 §E3);
        /// per-request API messages are localized from Accept-Language, not this field.
        /// </summary>
        [MaxLength(16)]
        public string? Language { get; set; }

        /// <summary>
        /// Preferred distance display unit: "km" or "mi". Null means no explicit choice —
        /// the client resolves from browser locale, else kilometres (ADR 0002 Decision B).
        /// Inference never writes this column.
        /// </summary>
        [MaxLength(8)]
        public string? DistanceUnit { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

