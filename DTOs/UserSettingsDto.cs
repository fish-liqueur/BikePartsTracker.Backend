using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// DTO for reading user settings
    /// </summary>
    public class UserSettingsDto
    {
        public int DefaultChainCycleLength { get; set; }
        /// <summary>Default chain-cycle interval in metres.</summary>
        public int DefaultChainCycleIntervalMetres { get; set; }
        public bool defaultUseChainCycle { get; set; }
        public bool showTips { get; set; }

        /// <summary>
        /// Preferred language (BCP-47). Null when the rider has made no explicit choice
        /// (resolves to English on the client).
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Preferred distance unit ("km" | "mi"). Null when unset — client resolves via locale.
        /// </summary>
        public string? DistanceUnit { get; set; }
    }

    /// <summary>
    /// DTO for updating user settings (all properties nullable for partial updates)
    /// </summary>
    public class UpdateUserSettingsDto
    {
        public int? DefaultChainCycleLength { get; set; }
        /// <summary>Default chain-cycle interval in metres.</summary>
        public int? DefaultChainCycleIntervalMetres { get; set; }
        public bool? defaultUseChainCycle { get; set; }
        public bool? showTips { get; set; }

        /// <summary>
        /// Preferred language. Must be one of the supported launch locales (ADR 0006) or null.
        /// Rejected via the standard COMMON_VALIDATION envelope when it isn't.
        /// </summary>
        [RegularExpression("^(en|de|ru|uk)$", ErrorMessage = "Language must be one of: en, de, ru, uk.")]
        public string? Language { get; set; }

        /// <summary>
        /// Preferred distance unit. Must be "km", "mi", or null (clear explicit preference).
        /// Setter tracks JSON presence so omitted ≠ explicit null (partial update).
        /// </summary>
        [RegularExpression("^(km|mi)$", ErrorMessage = "DistanceUnit must be one of: km, mi.")]
        public string? DistanceUnit
        {
            get => _distanceUnit;
            set
            {
                _distanceUnit = value;
                DistanceUnitSpecified = true;
            }
        }

        [JsonIgnore]
        public bool DistanceUnitSpecified { get; private set; }

        private string? _distanceUnit;
    }
}
