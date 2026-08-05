using System.Text.Json.Serialization;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Thin Strava webhook event payload (POST /api/strava/webhook).
    /// </summary>
    public class StravaWebhookEventDto
    {
        [JsonPropertyName("object_type")]
        public string ObjectType { get; set; } = string.Empty;

        [JsonPropertyName("object_id")]
        public long ObjectId { get; set; }

        [JsonPropertyName("aspect_type")]
        public string AspectType { get; set; } = string.Empty;

        [JsonPropertyName("owner_id")]
        public long OwnerId { get; set; }

        [JsonPropertyName("subscription_id")]
        public long SubscriptionId { get; set; }

        [JsonPropertyName("event_time")]
        public long EventTime { get; set; }

        [JsonPropertyName("updates")]
        public Dictionary<string, string>? Updates { get; set; }
    }
}
