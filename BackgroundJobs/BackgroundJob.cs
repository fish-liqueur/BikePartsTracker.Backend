namespace BikePartsTracker.BackgroundJobs
{
    public enum BackgroundJobKind
    {
        ProcessStravaWebhook = 1,
        GapFillAutoImport = 2
    }

    public sealed class BackgroundJob
    {
        public BackgroundJobKind Kind { get; init; }
        public int Attempt { get; init; } = 1;

        /// <summary>Strava athlete id (owner_id) for webhook jobs.</summary>
        public long? OwnerId { get; init; }

        /// <summary>Strava object type: "activity" or "athlete".</summary>
        public string? ObjectType { get; init; }

        /// <summary>Strava aspect type: "create", "update", or "delete".</summary>
        public string? AspectType { get; init; }

        /// <summary>Strava object id (activity id or athlete id).</summary>
        public long? ObjectId { get; init; }

        /// <summary>Optional Strava event updates payload (e.g. authorized=false).</summary>
        public Dictionary<string, string>? Updates { get; init; }

        /// <summary>User who triggered gap-fill.</summary>
        public Guid? UserId { get; init; }

        /// <summary>Gap-fill range start (UTC date).</summary>
        public DateTime? RangeFrom { get; init; }

        /// <summary>Gap-fill range end (UTC date).</summary>
        public DateTime? RangeTo { get; init; }
    }
}
