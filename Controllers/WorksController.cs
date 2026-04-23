using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Extensions;
using BikePartsTracker.Models;
using BikePartsTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WorksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWorkEvaluationService _workEvaluationService;
        private readonly IWorkShadowPeriodService _workShadowPeriodService;

        public WorksController(
            AppDbContext context,
            IWorkEvaluationService workEvaluationService,
            IWorkShadowPeriodService workShadowPeriodService)
        {
            _context = context;
            _workEvaluationService = workEvaluationService;
            _workShadowPeriodService = workShadowPeriodService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkDto>>> Get([FromQuery] WorkParentType? parentType, [FromQuery] Guid? parentId)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var query = _context.Works.Where(w => w.UserId == userId);

            if (parentType.HasValue)
            {
                query = query.Where(w => w.ParentType == parentType.Value);
            }

            if (parentId.HasValue)
            {
                query = query.Where(w => w.ParentId == parentId.Value);
            }

            var works = await query.OrderByDescending(w => w.CreatedAt).ToListAsync();
            var results = new List<WorkDto>(works.Count);

            foreach (var work in works)
            {
                results.Add(await MapToDtoAsync(work));
            }

            return Ok(results);
        }

        [HttpPost]
        public async Task<ActionResult<WorkDto>> Create([FromBody] CreateWorkDto dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var validationError = await ValidateParentAsync(userId, dto.ParentType, dto.ParentId);
            if (validationError != null)
            {
                return BadRequest(new { message = validationError });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            var work = new Work
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                User = user,
                Name = dto.Name,
                Description = dto.Description,
                StartDate = dto.StartDate,
                Type = dto.Type,
                TriggerType = dto.TriggerType,
                ParentType = dto.ParentType,
                ParentId = dto.ParentId,
                TriggerValue = dto.TriggerValue,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Works.Add(work);
            await _context.SaveChangesAsync();
            await _workShadowPeriodService.SyncShadowPeriodsAsync(work);

            return CreatedAtAction(nameof(Get), new { id = work.Id }, await MapToDtoAsync(work));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WorkDto>> Update(Guid id, [FromBody] UpdateWorkDto dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var work = await _context.Works.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
            if (work == null)
            {
                return NotFound();
            }

            if (dto.ParentType.HasValue || dto.ParentId.HasValue)
            {
                var targetParentType = dto.ParentType ?? work.ParentType;
                var targetParentId = dto.ParentId ?? work.ParentId;
                var validationError = await ValidateParentAsync(userId, targetParentType, targetParentId);
                if (validationError != null)
                {
                    return BadRequest(new { message = validationError });
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                work.Name = dto.Name;
            }

            if (dto.Description != null)
            {
                work.Description = dto.Description;
            }

            if (dto.StartDate.HasValue)
            {
                work.StartDate = dto.StartDate.Value;
            }

            if (dto.Type.HasValue)
            {
                work.Type = dto.Type.Value;
            }

            if (dto.TriggerType.HasValue)
            {
                work.TriggerType = dto.TriggerType.Value;
            }

            if (dto.ParentType.HasValue)
            {
                work.ParentType = dto.ParentType.Value;
            }

            if (dto.ParentId.HasValue)
            {
                work.ParentId = dto.ParentId.Value;
            }

            if (dto.TriggerValue.HasValue)
            {
                work.TriggerValue = dto.TriggerValue.Value;
            }

            if (dto.IsActive.HasValue)
            {
                work.IsActive = dto.IsActive.Value;
            }

            work.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _workShadowPeriodService.SyncShadowPeriodsAsync(work);

            return Ok(await MapToDtoAsync(work));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var work = await _context.Works.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
            if (work == null)
            {
                return NotFound();
            }

            _context.Works.Remove(work);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<WorkDto> MapToDtoAsync(Work work)
        {
            var consumed = await _workEvaluationService.GetConsumedValueAsync(work);
            var remaining = Math.Max(0, work.TriggerValue - consumed);
            var needsAttention = consumed >= work.TriggerValue;

            return new WorkDto
            {
                Id = work.Id,
                Name = work.Name,
                Description = work.Description,
                StartDate = work.StartDate,
                Type = work.Type,
                TriggerType = work.TriggerType,
                ParentType = work.ParentType,
                ParentId = work.ParentId,
                TriggerValue = work.TriggerValue,
                IsActive = work.IsActive,
                ConsumedValue = consumed,
                RemainingValue = remaining,
                NeedsAttention = needsAttention
            };
        }

        private async Task<string?> ValidateParentAsync(Guid userId, WorkParentType parentType, Guid parentId)
        {
            return parentType switch
            {
                WorkParentType.Part => await _context.BikeParts.AnyAsync(p => p.Id == parentId && p.UserId == userId)
                    ? null
                    : "Part parent not found for user.",
                WorkParentType.Bike => await _context.Bikes.AnyAsync(b => b.Id == parentId && b.UserId == userId)
                    ? null
                    : "Bike parent not found for user.",
                WorkParentType.ChainCycle => await _context.ChainCycles
                    .AnyAsync(c => c.Id == parentId && c.Bike.UserId == userId)
                    ? null
                    : "Chain cycle parent not found for user.",
                _ => "Unsupported parent type."
            };
        }

    }
}
