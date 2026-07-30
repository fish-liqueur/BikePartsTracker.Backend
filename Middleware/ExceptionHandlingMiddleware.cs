using System.Collections.Generic;
using System.Text.Json;
using BikePartsTracker.Exceptions;
using BikePartsTracker.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BikePartsTracker.Middleware
{
    /// <summary>
    /// Global error handling (ADR 0006 §E1): converts <see cref="AppException"/> and any unhandled
    /// exception into a single RFC 9457 Problem Details body, extended with a stable <c>code</c> and
    /// flat <c>params</c>, and a server-rendered localized <c>detail</c>. Sits after
    /// <c>UseRequestLocalization</c> so the culture is already resolved when an error unwinds through it.
    /// This also converges the ad-hoc error shapes used across controllers onto one envelope.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (AppException ex)
            {
                await WriteProblemAsync(context, ex.StatusCode, ex.Code, ex.Params);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    ErrorCodes.CommonUnexpected,
                    new Dictionary<string, object?>());
            }
        }

        private static async Task WriteProblemAsync(
            HttpContext context,
            int statusCode,
            string code,
            IReadOnlyDictionary<string, object?> @params)
        {
            if (context.Response.HasStarted)
            {
                // Too late to shape the response; let it surface as-is.
                return;
            }

            var factory = context.RequestServices.GetRequiredService<ILocalizedErrorFactory>();
            var detail = factory.Resolve(code, @params);

            var problem = new Dictionary<string, object?>
            {
                ["type"] = "about:blank",
                ["title"] = ReasonPhrases.GetReasonPhrase(statusCode),
                ["status"] = statusCode,
                ["code"] = code,
                ["params"] = @params,
                ["detail"] = detail,
            };

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
