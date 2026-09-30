using System.Globalization;
using System.Text;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

/// <summary>
/// Culture-independent text helpers shared by the category matcher, the type reader and the duplicate
/// description key (F-020, DD-7, DD-8). Nothing here consults the current culture.
/// </summary>
public static class ImportText
{
    /// <summary>Trims the text and collapses every run of whitespace (including NBSP and control separators) to one space.</summary>
    /// <param name="value">The raw text.</param>
    public static string CollapseWhitespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        StringBuilder builder = new(value.Length);
        bool pendingSpace = false;

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// The description part of a duplicate fingerprint: collapsed whitespace and invariant upper case. Diacritics
    /// and punctuation are kept on purpose, so a false duplicate (which would silently drop a real row) stays unlikely.
    /// </summary>
    /// <param name="description">The description.</param>
    public static string DescriptionKey(string description) =>
        CollapseWhitespace(description).ToUpperInvariant();

    /// <summary>
    /// Trimmed, diacritics-free, invariant upper-case form used to recognise fixed vocabulary such as the type
    /// synonyms. Unlike the category fold it does not strip quotes or trailing punctuation.
    /// </summary>
    /// <param name="value">The raw text.</param>
    public static string LooseKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().ToUpperInvariant();
    }
}
