using System.Collections;
using System.Collections.Generic;
using BikePartsTracker.Localization;

namespace BikePartsTracker.Exceptions
{
    /// <summary>
    /// A domain/application error that carries a stable machine <see cref="Code"/> and structured
    /// <see cref="Params"/> (ADR 0006 §E1). Thrown by services/controllers and translated to an
    /// RFC 9457 Problem Details response — with a localized <c>detail</c> resolved from the backend
    /// catalog by <see cref="Code"/> — by the global exception-handling middleware.
    /// </summary>
    public class AppException : Exception
    {
        /// <summary>Stable machine code (<c>SCREAMING_SNAKE_CASE</c>); see <see cref="ErrorCodes"/>.</summary>
        public string Code { get; }

        /// <summary>HTTP status this error maps to.</summary>
        public int StatusCode { get; }

        /// <summary>Flat interpolation params (scalars). Names are part of the API contract.</summary>
        public IReadOnlyDictionary<string, object?> Params { get; }

        public AppException(string code, object? @params = null, int? statusCode = null)
            : base(code)
        {
            Code = code;
            Params = ToDictionary(@params);
            StatusCode = statusCode ?? ErrorCodes.DefaultStatusFor(code);
        }

        public static AppException NotFound(object? @params = null) =>
            new(ErrorCodes.CommonNotFound, @params);

        public static AppException Forbidden(object? @params = null) =>
            new(ErrorCodes.CommonForbidden, @params);

        private static IReadOnlyDictionary<string, object?> ToDictionary(object? @params)
        {
            if (@params is null)
            {
                return EmptyParams;
            }

            if (@params is IReadOnlyDictionary<string, object?> ready)
            {
                return ready;
            }

            if (@params is IDictionary dictionary)
            {
                var fromDict = new Dictionary<string, object?>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                    {
                        fromDict[entry.Key.ToString()!] = entry.Value;
                    }
                }
                return fromDict;
            }

            // Anonymous object (e.g. new { max = 200 }) → reflect its public properties.
            var result = new Dictionary<string, object?>();
            foreach (var prop in @params.GetType().GetProperties())
            {
                result[prop.Name] = prop.GetValue(@params);
            }
            return result;
        }

        private static readonly IReadOnlyDictionary<string, object?> EmptyParams =
            new Dictionary<string, object?>();
    }
}
