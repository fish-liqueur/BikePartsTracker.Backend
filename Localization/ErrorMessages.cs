namespace BikePartsTracker.Backend.Localization
{
    /// <summary>
    /// Marker type for the localized error-message catalog (ADR 0006 §E2). Resolved against the
    /// resource files at <c>Resources/Localization/ErrorMessages.{culture}.resx</c> via
    /// <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>. The neutral (keyless)
    /// resource is English and is the ultimate fallback.
    ///
    /// The namespace mirrors the resx folder under the assembly root (assembly name is
    /// <c>BikePartsTracker.Backend</c>), so the <c>IStringLocalizer&lt;T&gt;</c> base name resolves to
    /// the embedded resource <c>BikePartsTracker.Backend.Resources.Localization.ErrorMessages</c>.
    /// </summary>
    public sealed class ErrorMessages
    {
    }
}
