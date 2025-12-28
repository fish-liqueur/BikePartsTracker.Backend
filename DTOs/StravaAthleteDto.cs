namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Data transfer object for Strava athlete information
    /// </summary>
    public class StravaAthleteDto
    {
        /// <summary>
        /// Strava athlete ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Athlete username
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Athlete first name
        /// </summary>
        public string? Firstname { get; set; }

        /// <summary>
        /// Athlete last name
        /// </summary>
        public string? Lastname { get; set; }

        /// <summary>
        /// Athlete city
        /// </summary>
        public string? City { get; set; }

        /// <summary>
        /// Athlete state
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Athlete country
        /// </summary>
        public string? Country { get; set; }

        /// <summary>
        /// List of bikes from Strava
        /// </summary>
        public List<StravaBikeDto> Bikes { get; set; } = new List<StravaBikeDto>();
    }
}

