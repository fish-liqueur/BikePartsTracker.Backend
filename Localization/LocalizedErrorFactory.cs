using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BikePartsTracker.Backend.Localization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace BikePartsTracker.Localization
{
    /// <summary>
    /// Resolves a ready-to-show, localized <c>detail</c> string for an error <c>code</c> in the
    /// caller's current culture, interpolating flat <c>params</c> (ADR 0006 §E1/§E2). English is the
    /// neutral fallback: a missing per-culture entry falls back to the English resource, and a code
    /// missing everywhere returns a stable placeholder (and, in non-production, logs a warning — §E5).
    /// </summary>
    public interface ILocalizedErrorFactory
    {
        string Resolve(string code, IReadOnlyDictionary<string, object?> @params);
    }

    public class LocalizedErrorFactory : ILocalizedErrorFactory
    {
        private readonly IStringLocalizer<ErrorMessages> _localizer;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<LocalizedErrorFactory> _logger;

        public LocalizedErrorFactory(
            IStringLocalizer<ErrorMessages> localizer,
            IHostEnvironment environment,
            ILogger<LocalizedErrorFactory> logger)
        {
            _localizer = localizer;
            _environment = environment;
            _logger = logger;
        }

        public string Resolve(string code, IReadOnlyDictionary<string, object?> @params)
        {
            var localized = _localizer[code];

            if (localized.ResourceNotFound)
            {
                // Missing in every culture including the English neutral resource. Never surface a raw
                // key to the rider; log loudly outside production so devs/QA see the gap (E5, B-15).
                if (!_environment.IsProduction())
                {
                    _logger.LogWarning(
                        "Localized error message missing for code {ErrorCode} in culture {Culture} (and neutral). Returning a generic fallback.",
                        code,
                        CultureInfo.CurrentUICulture.Name);
                }

                return "An error occurred.";
            }

            return Interpolate(localized.Value, @params);
        }

        /// <summary>
        /// Replaces <c>{name}</c> tokens with the matching param value (formatted in the current
        /// culture). Unknown tokens are left as-is; this is intentionally simpler than
        /// <c>string.Format</c> so param names — not positions — are the contract.
        /// </summary>
        private static string Interpolate(string template, IReadOnlyDictionary<string, object?> @params)
        {
            if (@params.Count == 0 || template.IndexOf('{') < 0)
            {
                return template;
            }

            var result = new StringBuilder(template.Length);
            for (var i = 0; i < template.Length; i++)
            {
                var c = template[i];
                if (c != '{')
                {
                    result.Append(c);
                    continue;
                }

                var end = template.IndexOf('}', i + 1);
                if (end < 0)
                {
                    result.Append(template, i, template.Length - i);
                    break;
                }

                var name = template.Substring(i + 1, end - i - 1);
                if (@params.TryGetValue(name, out var value))
                {
                    result.Append(FormatValue(value));
                }
                else
                {
                    result.Append(template, i, end - i + 1);
                }
                i = end;
            }

            return result.ToString();
        }

        private static string FormatValue(object? value) => value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
