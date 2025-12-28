namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Data transfer object for a bike from Strava API
    /// </summary>
    public class StravaBikeDto
    {
        /// <summary>
        /// Strava bike ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is the primary bike
        /// </summary>
        public bool Primary { get; set; }

        /// <summary>
        /// Bike name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Total distance in meters
        /// </summary>
        public double Distance { get; set; }
    }
}

