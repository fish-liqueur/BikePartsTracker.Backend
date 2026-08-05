using BikePartsTracker.BackgroundJobs;
using BikePartsTracker.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BikePartsTracker.Controllers
{
    /// <summary>
    /// Public Strava push-subscription ingress. Explicitly anonymous (outside authenticated data surface).
    /// </summary>
    [ApiController]
    [Route("api/strava/webhook")]
    [AllowAnonymous]
    public class StravaWebhookController : ControllerBase
    {
        private readonly IBackgroundJobQueue _jobQueue;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StravaWebhookController> _logger;

        public StravaWebhookController(
            IBackgroundJobQueue jobQueue,
            IConfiguration configuration,
            ILogger<StravaWebhookController> logger)
        {
            _jobQueue = jobQueue;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Strava hub validation (subscription handshake).
        /// </summary>
        [HttpGet]
        public IActionResult Validate(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.challenge")] string? challenge,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken)
        {
            var expected = _configuration["Strava:WebhookVerifyToken"];
            if (string.IsNullOrEmpty(expected) ||
                string.IsNullOrEmpty(verifyToken) ||
                !string.Equals(verifyToken, expected, StringComparison.Ordinal) ||
                !string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(challenge))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            return Ok(new Dictionary<string, string> { ["hub.challenge"] = challenge });
        }

        /// <summary>
        /// Accept a thin event, enqueue, and ACK 200 immediately (Strava 2s rule).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Receive([FromBody] StravaWebhookEventDto? evt, CancellationToken cancellationToken)
        {
            if (evt != null &&
                !string.IsNullOrWhiteSpace(evt.ObjectType) &&
                !string.IsNullOrWhiteSpace(evt.AspectType))
            {
                await _jobQueue.EnqueueAsync(new BackgroundJob
                {
                    Kind = BackgroundJobKind.ProcessStravaWebhook,
                    OwnerId = evt.OwnerId,
                    ObjectType = evt.ObjectType,
                    AspectType = evt.AspectType,
                    ObjectId = evt.ObjectId,
                    Updates = evt.Updates
                }, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Received empty or invalid Strava webhook payload; acknowledging anyway.");
            }

            return Ok();
        }
    }
}
