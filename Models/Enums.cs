namespace BikePartsTracker.Models
{
    public enum PartScheduleType
    {
        OneTimeUse = 1,         // e.g., brake pads, tyres
        IntervalMaintenance = 2,// e.g., chain lube
        CyclicReplacement = 3   // e.g., chain replacement every N km
    }
}