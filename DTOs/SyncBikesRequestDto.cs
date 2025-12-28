using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Data transfer object for syncing bikes from Strava
    /// </summary>
    public class SyncBikesRequestDto
    {
        [Required]
        public List<SyncBikeDto> Bikes { get; set; } = new List<SyncBikeDto>();
    }
}

