using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// DTO for reading user settings
    /// </summary>
    public class UserSettingsDto
    {
        public int DefaultChainCycleLength { get; set; }
        public int DefaultChainCycleIntervalKm { get; set; }
        public bool defaultUseChainCycle { get; set; }
        public bool showTips { get; set; }

        /// <summary>
        /// Preferred language (BCP-47). Null when the rider has made no explicit choice
        /// (resolves to English on the client).
        /// </summary>
        public string? Language { get; set; }
    }

    /// <summary>
    /// DTO for updating user settings (all properties nullable for partial updates)
    /// </summary>
    public class UpdateUserSettingsDto
    {
        public int? DefaultChainCycleLength { get; set; }
        public int? DefaultChainCycleIntervalKm { get; set; }
        public bool? defaultUseChainCycle { get; set; }
        public bool? showTips { get; set; }

        /// <summary>
        /// Preferred language. Must be one of the supported launch locales (ADR 0006) or null.
        /// Rejected via the standard COMMON_VALIDATION envelope when it isn't.
        /// </summary>
        [RegularExpression("^(en|de|ru|uk)$", ErrorMessage = "Language must be one of: en, de, ru, uk.")]
        public string? Language { get; set; }
    }
}

