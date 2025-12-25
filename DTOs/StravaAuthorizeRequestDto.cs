using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Data transfer object for Strava authorization request
    /// </summary>
    public class StravaAuthorizeRequestDto
    {
        /// <summary>
        /// Authorization code from Strava OAuth redirect
        /// </summary>
        [Required]
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// The redirect URI used in OAuth flow
        /// </summary>
        [Required]
        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; } = string.Empty;
    }
}

