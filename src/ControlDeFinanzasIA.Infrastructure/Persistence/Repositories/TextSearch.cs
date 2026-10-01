namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Builds the <c>LIKE</c> pattern behind the list text search. The characters that act as wildcards in a
/// <c>LIKE</c> pattern are escaped so that what the user types is always searched literally.
/// </summary>
internal static class TextSearch
{
    /// <summary>The escape character to pass as the third argument of <c>EF.Functions.Like</c>.</summary>
    public const string EscapeCharacter = "\\";

    /// <summary>Builds a "contains" pattern for <paramref name="text"/>.</summary>
    /// <param name="text">The text typed by the user.</param>
    /// <returns>The pattern, e.g. <c>%50\%%</c> for <c>50%</c>.</returns>
    public static string ContainsPattern(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string escaped = text
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);

        return "%" + escaped + "%";
    }
}
