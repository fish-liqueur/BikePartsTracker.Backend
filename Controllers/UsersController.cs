using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BikePartsTracker.Data;
using BikePartsTracker.Models;
using BikePartsTracker.DTOs;
using BikePartsTracker.Extensions;

namespace BikePartsTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get user settings for the authenticated user
        /// </summary>
        /// <returns>User settings</returns>
        /// <response code="200">Returns user settings</response>
        /// <response code="401">User not authenticated</response>
        /// <response code="404">User settings not found</response>
        [HttpGet("settings")]
        [ProducesResponseType(typeof(UserSettingsDto), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<UserSettingsDto>> GetUserSettings()
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var settings = await _context.UserSettings
                .FirstOrDefaultAsync(us => us.UserId == userId);

            // Create default settings if they don't exist
            if (settings == null)
            {
                settings = new UserSettings
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                    // DefaultChainCycleLength and DefaultChainCycleIntervalMetres
                    // will use their default values from the model
                };
                _context.UserSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return Ok(MapToDto(settings));
        }

        /// <summary>
        /// Update user settings for the authenticated user
        /// </summary>
        /// <param name="updateDto">Settings to update</param>
        /// <returns>Updated user settings</returns>
        /// <response code="200">Settings updated successfully</response>
        /// <response code="400">Invalid request data</response>
        /// <response code="401">User not authenticated</response>
        [HttpPut("settings")]
        [ProducesResponseType(typeof(UserSettingsDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<UserSettingsDto>> UpdateUserSettings([FromBody] UpdateUserSettingsDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            // Verify user exists
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            // Find existing settings or create new
            var settings = await _context.UserSettings
                .FirstOrDefaultAsync(us => us.UserId == userId);

            if (settings == null)
            {
                // Create new settings
                settings = new UserSettings
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UserSettings.Add(settings);
            }
            else
            {
                settings.UpdatedAt = DateTime.UtcNow;
            }

            if (updateDto.DefaultChainCycleLength.HasValue)
                settings.DefaultChainCycleLength = updateDto.DefaultChainCycleLength.Value;
            if (updateDto.DefaultChainCycleIntervalMetres.HasValue)
                settings.DefaultChainCycleIntervalMetres = updateDto.DefaultChainCycleIntervalMetres.Value;
            if (updateDto.defaultUseChainCycle.HasValue)
                settings.defaultUseChainCycle = updateDto.defaultUseChainCycle.Value;
            if (updateDto.showTips.HasValue)
                settings.showTips = updateDto.showTips.Value;
            if (updateDto.Language != null)
                settings.Language = updateDto.Language;
            if (updateDto.DistanceUnitSpecified)
                settings.DistanceUnit = updateDto.DistanceUnit;

            await _context.SaveChangesAsync();

            return Ok(MapToDto(settings));
        }

        private static UserSettingsDto MapToDto(UserSettings settings) => new()
        {
            DefaultChainCycleLength = settings.DefaultChainCycleLength,
            DefaultChainCycleIntervalMetres = settings.DefaultChainCycleIntervalMetres,
            defaultUseChainCycle = settings.defaultUseChainCycle,
            showTips = settings.showTips,
            Language = settings.Language,
            DistanceUnit = settings.DistanceUnit
        };
    }
}
