namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// The schema migrations known to the running application (F-019, R-9). Used to decide
/// whether a backup comes from a newer version.
/// </summary>
public interface IMigrationCatalog
{
    /// <summary>Every known <c>MigrationId</c>, ordinal-sorted ascending.</summary>
    IReadOnlyList<string> KnownMigrationIds { get; }
}
