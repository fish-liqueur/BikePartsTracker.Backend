namespace BikePartsTracker.Models
{
    public enum PartScheduleType
    {
        OneTimeUse = 1,         // e.g., brake pads, tyres
        IntervalMaintenance = 2,// e.g., chain lube
        CyclicReplacement = 3   // e.g., chain replacement every N km
    }

    public enum ExternalServiceType
    {
        Strava = 1,
        Komoot = 2,
        Garmin = 3
        // Add more services as needed
    }

    public enum PartType
    {
        Chain,
        Cassette,
        Chainring,
        BrakePads,
        Tyre,
        Battery,
        BottomBracket,
        Headset,
        Hub,
        Pedals,
        Other
    }

    public enum BikeType
    {
        Road,
        Mountain,
        Gravel,
        EBike,
        City,
        Touring,
        Cargo,
        Fixed,
        Rat,
        Other
    }

    public enum WorkType
    {
        OneTime = 1,
        Repeating = 2,
        Cyclic = 3
    }

    public enum WorkTriggerType
    {
        Distance = 1,
        Time = 2
    }

    public enum WorkParentType
    {
        Part = 1,
        Bike = 2,
        ChainCycle = 3
    }
}