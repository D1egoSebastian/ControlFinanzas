using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Reads the catalogs and the already-stored transactions an import needs to resolve categories and
/// detect duplicates (F-020, DD-1). Implemented in Infrastructure.
/// </summary>
public interface IImportLookup
{
    /// <summary>Loads the lookup snapshot.</summary>
    /// <param name="minDate">Earliest date found in the file; <c>null</c> skips the existing-row query.</param>
    /// <param name="maxDate">Latest date found in the file; <c>null</c> skips the existing-row query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with an <c>ImportLookupSnapshot</c> value, or a <c>LookupFailed</c> failure.</returns>
    Task<Result> LoadAsync(DateOnly? minDate, DateOnly? maxDate, CancellationToken ct = default);
}
