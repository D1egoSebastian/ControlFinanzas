using QuestPDF.Infrastructure;

namespace ControlDeFinanzasIA.Infrastructure.Reports;

/// <summary>
/// The single place where QuestPDF global settings are assigned (F-018, R-11, D-10).
/// The Community license is eligible for this personal, zero-revenue app; see the banner in
/// <c>Directory.Packages.props</c> and re-read the license text on every version bump.
/// </summary>
internal static class QuestPdfConfiguration
{
    /// <summary>
    /// Applies the license and the glyph policy. Idempotent and thread-safe: it assigns constants,
    /// performs no network access and sends no telemetry.
    /// </summary>
    public static void Apply()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // User-typed descriptions may contain emoji or rare glyphs. With the check on, the
        // library throws and that user's export fails every time; off, a missing glyph renders
        // as a replacement character (D-10). Named CheckIfAllTextGlyphsAreAvailable before 2026.9.
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }
}
