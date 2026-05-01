using System.Text.Json.Serialization;

namespace BikePartsTracker.DTOs
{
    public class StravaActivityDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("sport_type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("gear_id")]
        public string? GearId { get; set; }

        [JsonPropertyName("start_date_local")]
        public DateTime StartDateLocal { get; set; }
    }
}
