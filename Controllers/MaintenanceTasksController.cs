using BikePartsTracker.Data;
using BikePartsTracker.DTOs;
using BikePartsTracker.Exceptions;
using BikePartsTracker.Extensions;
using BikePartsTracker.Localization;
using BikePartsTracker.Models;
using BikePartsTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BikePartsTracker.Controllers
{
    [ApiController]
    [Route("api/maintenance-tasks")]
    [Authorize]
    public class MaintenanceTasksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMaintenanceTaskEvaluationService _maintenanceTaskEvaluationService;
        private readonly IMaintenanceTaskShadowPeriodService _maintenanceTaskShadowPeriodService;

        public MaintenanceTasksController(
            AppDbContext context,
            IMaintenanceTaskEvaluationService maintenanceTaskEvaluationService,
            IMaintenanceTaskShadowPeriodService maintenanceTaskShadowPeriodService)
        {
            _context = context;
            _maintenanceTaskEvaluationService = maintenanceTaskEvaluationService;
            _maintenanceTaskShadowPeriodService = maintenanceTaskShadowPeriodService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MaintenanceTaskDto>>> Get(
            [FromQuery] MaintenanceTaskParentType? parentType,
            [FromQuery] Guid? parentId,
            [FromQuery] bool? isActive,
            [FromQuery] Guid? bikeId,
            [FromQuery] bool excludePartParents = false,
            [FromQuery] Guid? relatedToPartId = null)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            if (bikeId.HasValue && relatedToPartId.HasValue)
            {
                throw new AppException(ErrorCodes.CommonValidation);
            }

            IQueryable<MaintenanceTask> query = _context.MaintenanceTasks.Where(w => w.UserId == userId);

            if (bikeId.HasValue)
            {
                var bikeOwned = await _context.Bikes.AnyAsync(b => b.Id == bikeId.Value && b.UserId == userId);
                if (!bikeOwned)
                {
                    return NotFound();
                }

                var partIdsOnBike = await _context.BikeParts
                    .Where(p => p.BikeId == bikeId.Value && p.UserId == userId)
                    .Select(p => p.Id)
                    .ToListAsync();

                var cycleIdsOnBike = await _context.ChainCycles
                    .Where(c => c.BikeId == bikeId.Value && c.Bike.UserId == userId)
                    .Select(c => c.Id)
                    .ToListAsync();

                query = query.Where(w =>
                    (w.ParentType == MaintenanceTaskParentType.Bike && w.ParentId == bikeId.Value)
                    || (!excludePartParents
                        && w.ParentType == MaintenanceTaskParentType.Part
                        && partIdsOnBike.Contains(w.ParentId))
                    || (w.ParentType == MaintenanceTaskParentType.ChainCycle
                        && cycleIdsOnBike.Contains(w.ParentId)));
            }
            else if (relatedToPartId.HasValue)
            {
                var part = await _context.BikeParts
                    .FirstOrDefaultAsync(p => p.Id == relatedToPartId.Value && p.UserId == userId);
                if (part == null)
                {
                    return NotFound();
                }

                var cycleIdsWithPart = await FindCycleIdsContainingPartAsync(relatedToPartId.Value, userId);

                query = query.Where(w =>
                    (w.ParentType == MaintenanceTaskParentType.Part && w.ParentId == relatedToPartId.Value)
                    || (w.ParentType == MaintenanceTaskParentType.ChainCycle
                        && cycleIdsWithPart.Contains(w.ParentId)));
            }
            else
            {
                if (parentType.HasValue)
                {
                    query = query.Where(w => w.ParentType == parentType.Value);
                }

                if (parentId.HasValue)
                {
                    query = query.Where(w => w.ParentId == parentId.Value);
                }
            }

            if (isActive.HasValue)
            {
                query = query.Where(w => w.IsActive == isActive.Value);
            }

            var maintenanceTasks = await query.OrderByDescending(w => w.CreatedAt).ToListAsync();
            var results = new List<MaintenanceTaskDto>(maintenanceTasks.Count);

            foreach (var maintenanceTask in maintenanceTasks)
            {
                results.Add(await MapToDtoAsync(maintenanceTask));
            }

            return Ok(results);
        }

        [HttpPost]
        public async Task<ActionResult<MaintenanceTaskDto>> Create([FromBody] CreateMaintenanceTaskDto dto)
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

            var maintenanceTask = new MaintenanceTask
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

            _context.MaintenanceTasks.Add(maintenanceTask);
            await _context.SaveChangesAsync();
            await _maintenanceTaskShadowPeriodService.SyncShadowPeriodsAsync(maintenanceTask);

            return CreatedAtAction(nameof(Get), new { id = maintenanceTask.Id }, await MapToDtoAsync(maintenanceTask));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<MaintenanceTaskDto>> Update(Guid id, [FromBody] UpdateMaintenanceTaskDto dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var maintenanceTask = await _context.MaintenanceTasks.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
            if (maintenanceTask == null)
            {
                return NotFound();
            }

            if (dto.ParentType.HasValue || dto.ParentId.HasValue)
            {
                var targetParentType = dto.ParentType ?? maintenanceTask.ParentType;
                var targetParentId = dto.ParentId ?? maintenanceTask.ParentId;
                var validationError = await ValidateParentAsync(userId, targetParentType, targetParentId);
                if (validationError != null)
                {
                    return BadRequest(new { message = validationError });
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                maintenanceTask.Name = dto.Name;
            }

            if (dto.Description != null)
            {
                maintenanceTask.Description = dto.Description;
            }

            if (dto.StartDate.HasValue)
            {
                maintenanceTask.StartDate = dto.StartDate.Value;
            }

            if (dto.Type.HasValue)
            {
                maintenanceTask.Type = dto.Type.Value;
            }

            if (dto.TriggerType.HasValue)
            {
                maintenanceTask.TriggerType = dto.TriggerType.Value;
            }

            if (dto.ParentType.HasValue)
            {
                maintenanceTask.ParentType = dto.ParentType.Value;
            }

            if (dto.ParentId.HasValue)
            {
                maintenanceTask.ParentId = dto.ParentId.Value;
            }

            if (dto.TriggerValue.HasValue)
            {
                maintenanceTask.TriggerValue = dto.TriggerValue.Value;
            }

            if (dto.IsActive.HasValue)
            {
                maintenanceTask.IsActive = dto.IsActive.Value;
            }

            maintenanceTask.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _maintenanceTaskShadowPeriodService.SyncShadowPeriodsAsync(maintenanceTask);

            return Ok(await MapToDtoAsync(maintenanceTask));
        }

        /// <summary>ADR 0011 — acknowledge an occurrence ("I did it!" / "Do it now").</summary>
        [HttpPost("{id}/acknowledge")]
        public async Task<ActionResult<AcknowledgeMaintenanceTaskResponseDto>> Acknowledge(
            Guid id,
            [FromBody] AcknowledgeMaintenanceTaskDto? dto)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            dto ??= new AcknowledgeMaintenanceTaskDto();

            var maintenanceTask = await _context.MaintenanceTasks
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
            if (maintenanceTask == null)
            {
                return NotFound();
            }

            if (!maintenanceTask.IsActive)
            {
                if (maintenanceTask.Type == MaintenanceTaskType.OneTime)
                {
                    throw new AppException(ErrorCodes.MaintenanceTaskAlreadyCompleted);
                }

                throw new AppException(ErrorCodes.MaintenanceTaskInactive);
            }

            var consumed = await _maintenanceTaskEvaluationService.GetConsumedValueAsync(maintenanceTask);
            if (consumed < maintenanceTask.TriggerValue && !dto.Force)
            {
                throw new AppException(ErrorCodes.MaintenanceTaskNotDue);
            }

            var now = DateTime.UtcNow;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (maintenanceTask.Type == MaintenanceTaskType.OneTime)
                {
                    maintenanceTask.IsActive = false;
                    maintenanceTask.UpdatedAt = now;
                }
                else
                {
                    // Repeating and Cyclic: reset measurement window (ADR 0012 swap not in scope).
                    maintenanceTask.StartDate = now;
                    maintenanceTask.UpdatedAt = now;
                }

                await _context.SaveChangesAsync();
                await _maintenanceTaskShadowPeriodService.SyncShadowPeriodsAsync(maintenanceTask);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return Ok(new AcknowledgeMaintenanceTaskResponseDto
            {
                MaintenanceTask = await MapToDtoAsync(maintenanceTask),
                Affected = new RideMutationResultDto
                {
                    AffectedMaintenanceTaskIds = new List<Guid> { maintenanceTask.Id }
                }
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!User.TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var maintenanceTask = await _context.MaintenanceTasks.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
            if (maintenanceTask == null)
            {
                return NotFound();
            }

            _context.MaintenanceTasks.Remove(maintenanceTask);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<List<Guid>> FindCycleIdsContainingPartAsync(Guid partId, Guid userId)
        {
            var cycles = await _context.ChainCycles
                .Where(c => c.Bike.UserId == userId)
                .ToListAsync();

            return cycles
                .Where(c => c.Chains.Any(chainId => chainId == partId))
                .Select(c => c.Id)
                .ToList();
        }

        private async Task<MaintenanceTaskDto> MapToDtoAsync(MaintenanceTask maintenanceTask)
        {
            var consumed = await _maintenanceTaskEvaluationService.GetConsumedValueAsync(maintenanceTask);
            var remaining = Math.Max(0, maintenanceTask.TriggerValue - consumed);
            var needsAttention = consumed >= maintenanceTask.TriggerValue;

            return new MaintenanceTaskDto
            {
                Id = maintenanceTask.Id,
                Name = maintenanceTask.Name,
                Description = maintenanceTask.Description,
                StartDate = maintenanceTask.StartDate,
                Type = maintenanceTask.Type,
                TriggerType = maintenanceTask.TriggerType,
                ParentType = maintenanceTask.ParentType,
                ParentId = maintenanceTask.ParentId,
                TriggerValue = maintenanceTask.TriggerValue,
                IsActive = maintenanceTask.IsActive,
                ConsumedValue = consumed,
                RemainingValue = remaining,
                NeedsAttention = needsAttention
            };
        }

        private async Task<string?> ValidateParentAsync(Guid userId, MaintenanceTaskParentType parentType, Guid parentId)
        {
            return parentType switch
            {
                MaintenanceTaskParentType.Part => await _context.BikeParts.AnyAsync(p => p.Id == parentId && p.UserId == userId)
                    ? null
                    : "Part parent not found for user.",
                MaintenanceTaskParentType.Bike => await _context.Bikes.AnyAsync(b => b.Id == parentId && b.UserId == userId)
                    ? null
                    : "Bike parent not found for user.",
                MaintenanceTaskParentType.ChainCycle => await _context.ChainCycles
                    .AnyAsync(c => c.Id == parentId && c.Bike.UserId == userId)
                    ? null
                    : "Chain cycle parent not found for user.",
                _ => "Unsupported parent type."
            };
        }

    }
}
