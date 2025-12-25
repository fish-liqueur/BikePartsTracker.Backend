namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Data transfer object for Strava authorization response
    /// </summary>
    public class StravaAuthResponseDto
    {
        /// <summary>
        /// Indicates if the authorization was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Response message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Strava athlete information (optional)
        /// </summary>
        public StravaAthleteDto? Athlete { get; set; }
    }
}

