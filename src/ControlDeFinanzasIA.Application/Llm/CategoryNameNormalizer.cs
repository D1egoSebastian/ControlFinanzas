using System.Globalization;
using System.Text;

namespace ControlDeFinanzasIA.Application.Llm;

/// <summary>
/// Normalización de nombres de categoría usada por el caso de uso de
/// categorización automática para el emparejamiento en dos niveles (F-013,
/// slice 5a, D-6). No conoce categorías ni repositorios — es texto puro.
/// </summary>
public static class CategoryNameNormalizer
{
    /// <summary>
    /// Clave de nivel 1: recortada, espacios internos colapsados a uno
    /// solo, y comillas/puntuación envolventes eliminadas. Sensible a
    /// mayúsculas/minúsculas y a diacríticos — el nivel 1 de
    /// emparejamiento usa <see cref="StringComparison.OrdinalIgnoreCase"/>
    /// sobre este valor.
    /// </summary>
    /// <param name="value">Nombre crudo a limpiar.</param>
    public static string Clean(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string collapsed = CollapseWhitespace(value.Trim());
        string unquoted = StripSurroundingQuotes(collapsed);
        return unquoted.TrimEnd('.', ',', ';', ':', '!', '?');
    }

    /// <summary>
    /// Clave de nivel 2: <see cref="Clean"/> con diacríticos eliminados
    /// (NFD Unicode, se descartan los <see cref="UnicodeCategory.NonSpacingMark"/>)
    /// y mayusculizado invariante-de-cultura, de modo que
    /// <c>Fold("alimentacion") == Fold("Alimentación")</c>. Esto NO es
    /// opcional en una app <c>es-DO</c>: un modelo preguntado en español
    /// omite acentos con la misma frecuencia con la que los conserva.
    /// </summary>
    /// <param name="value">Nombre crudo a normalizar.</param>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string cleaned = Clean(value);
        string decomposed = cleaned.Normalize(NormalizationForm.FormD);

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

    private static string CollapseWhitespace(string value)
    {
        StringBuilder builder = new(value.Length);
        bool lastWasWhitespace = false;

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasWhitespace)
                {
                    builder.Append(' ');
                }

                lastWasWhitespace = true;
            }
            else
            {
                builder.Append(c);
                lastWasWhitespace = false;
            }
        }

        return builder.ToString();
    }

    private static string StripSurroundingQuotes(string value)
    {
        if (value.Length >= 2)
        {
            char first = value[0];
            char last = value[^1];
            bool isQuotePair = (first == '"' && last == '"') || (first == '\'' && last == '\'');

            if (isQuotePair)
            {
                return value[1..^1].Trim();
            }
        }

        return value;
    }
}
