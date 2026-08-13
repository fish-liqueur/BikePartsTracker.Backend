using Microsoft.AspNetCore.Http;

namespace BikePartsTracker.Localization
{
    /// <summary>
    /// The stable machine error codes (ADR 0006 §E1). These are <c>SCREAMING_SNAKE_CASE</c>,
    /// never translated, safe for clients to branch on, and used as the keys into
    /// <see cref="ErrorMessages"/>. Add a matching entry to every <c>ErrorMessages.*.resx</c> when
    /// introducing a new code (English is the source of truth; other locales fall back to it).
    /// </summary>
    public static class ErrorCodes
    {
        public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
        public const string AuthEmailTaken = "AUTH_EMAIL_TAKEN";
        public const string RidesEndDateBeforeStartDate = "RIDES_ENDDATE_BEFORE_STARTDATE";
        public const string RidesStravaNotConnected = "RIDES_STRAVA_NOT_CONNECTED";
        public const string BikesDuplicateStravaId = "BIKES_DUPLICATE_STRAVA_ID";
        /// <summary>A create/update referenced a bike that does not exist or is not owned by the caller (400).</summary>
        public const string BikesNotFound = "BIKES_NOT_FOUND";
        public const string PartsBatchLimitExceeded = "PARTS_BATCH_LIMIT_EXCEEDED";
        /// <summary>Fill-empty-slots called when the cycle has no empty slots (400).</summary>
        public const string ChainCyclesNoEmptySlots = "CHAIN_CYCLES_NO_EMPTY_SLOTS";
        /// <summary>activeNewSlotIndex is out of range or addresses a slot that is already filled (400).</summary>
        public const string ChainCyclesInvalidActiveSlot = "CHAIN_CYCLES_INVALID_ACTIVE_SLOT";
        /// <summary>Acknowledge called before due without <c>force: true</c> (400).</summary>
        public const string MaintenanceTaskNotDue = "MAINTENANCE_TASK_NOT_DUE";
        /// <summary>Acknowledge on an already-completed OneTime task (409).</summary>
        public const string MaintenanceTaskAlreadyCompleted = "MAINTENANCE_TASK_ALREADY_COMPLETED";
        /// <summary>Acknowledge on an inactive Repeating/Cyclic task (409).</summary>
        public const string MaintenanceTaskInactive = "MAINTENANCE_TASK_INACTIVE";
        public const string CommonNotFound = "COMMON_NOT_FOUND";
        public const string CommonForbidden = "COMMON_FORBIDDEN";
        public const string CommonValidation = "COMMON_VALIDATION";
        public const string CommonUnexpected = "COMMON_UNEXPECTED";

        /// <summary>
        /// Default HTTP status for a code, used when an <see cref="Exceptions.AppException"/> does not
        /// override it. Unknown codes default to 400.
        /// </summary>
        public static int DefaultStatusFor(string code) => code switch
        {
            AuthInvalidCredentials => StatusCodes.Status401Unauthorized,
            AuthEmailTaken => StatusCodes.Status409Conflict,
            MaintenanceTaskAlreadyCompleted => StatusCodes.Status409Conflict,
            MaintenanceTaskInactive => StatusCodes.Status409Conflict,
            CommonNotFound => StatusCodes.Status404NotFound,
            CommonForbidden => StatusCodes.Status403Forbidden,
            CommonUnexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest,
        };
    }
}
