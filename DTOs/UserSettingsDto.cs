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
    }
}

