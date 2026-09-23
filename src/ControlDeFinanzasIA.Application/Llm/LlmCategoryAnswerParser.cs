using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControlDeFinanzasIA.Application.Llm;

/// <summary>
/// Parser tolerante de la respuesta cruda de un <c>ILLMClient</c> para
/// categorización automática de gastos (F-013, slice 5a, D-5). Pipeline
/// fijo de cuatro pasos, gana el primer éxito:
/// <list type="number">
/// <item>Recortar y quitar el "cercado" de markdown (```json / ```).</item>
/// <item>
/// Extraer el primer objeto <c>{ … }</c> balanceado (contador de
/// profundidad consciente de literales de cadena y escapes) y parsearlo
/// como JSON, leyendo <c>category</c>/<c>categoria</c>/<c>name</c>
/// (comparación <see cref="StringComparison.OrdinalIgnoreCase"/>).
/// </item>
/// <item>
/// Si no hay JSON parseable, aceptar el texto limpio como nombre de
/// categoría SOLO si es de una sola línea y ≤
/// <see cref="MaxBareAnswerLength"/> caracteres.
/// </item>
/// <item>Cualquier otro caso → <see cref="LlmAnswerKind.Unparseable"/>.</item>
/// </list>
/// El parser NO conoce qué es una <c>Category</c>: nunca ve la lista de
/// categorías, nunca toca un <c>Guid</c>. El emparejamiento tolerante a
/// mayúsculas/diacríticos vive en <see cref="CategoryNameNormalizer"/> y en
/// el caso de uso, no aquí. NO se agrega fuzzy matching a este tipo — el
/// prompt ya entrega al modelo la lista cerrada de respuestas legales.
/// </summary>
public static class LlmCategoryAnswerParser
{
    /// <summary>Longitud máxima de una respuesta de texto plano aceptada como nombre de categoría (D-3 paso 3).</summary>
    public const int MaxBareAnswerLength = 60;

    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Anclada, no-greedy, y consciente de saltos de línea vía la clase
    /// <c>[\s\S]</c> en lugar de <see cref="RegexOptions.Singleline"/> — el
    /// diseño exige explícitamente <see cref="RegexOptions.None"/>.
    /// </summary>
    private static readonly Regex _fenceRegex = new(
        pattern: "^```(?:json)?\\s*(?<body>[\\s\\S]*?)\\s*```$",
        options: RegexOptions.None,
        matchTimeout: _regexTimeout);

    private static readonly string[] _categoryPropertyNames = ["category", "categoria", "name"];

    private static readonly string[] _noneTokens = ["NULL", "NONE", "NINGUNA"];

    /// <summary>Parsea la respuesta cruda del modelo en un <see cref="LlmCategoryAnswer"/>.</summary>
    /// <param name="rawContent">Contenido crudo devuelto por el proveedor. Puede ser <c>null</c>.</param>
    public static LlmCategoryAnswer Parse(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return Unparseable;
        }

        string cleaned = StripFence(rawContent.Trim());

        if (TryExtractBalancedJson(cleaned, out string jsonCandidate) &&
            TryParseJsonAnswer(jsonCandidate, out LlmCategoryAnswer jsonAnswer))
        {
            return jsonAnswer;
        }

        if (TryBareString(cleaned, out LlmCategoryAnswer bareAnswer))
        {
            return bareAnswer;
        }

        return Unparseable;
    }

    private static LlmCategoryAnswer Unparseable { get; } = new(LlmAnswerKind.Unparseable, null);

    private static LlmCategoryAnswer ExplicitNone { get; } = new(LlmAnswerKind.ExplicitNone, null);

    private static string StripFence(string trimmed)
    {
        Match match = _fenceRegex.Match(trimmed);
        return match.Success ? match.Groups["body"].Value.Trim() : trimmed;
    }

    /// <summary>
    /// Escanea el primer objeto <c>{ … }</c> balanceado, con un contador de
    /// profundidad consciente de literales de cadena y de secuencias de
    /// escape. Un <c>IndexOf('{')</c>/<c>LastIndexOf('}')</c> naive, o un
    /// contador de profundidad ciego a cadenas, mal-cortaría
    /// <c>{"category":"a} b"}</c> en la primera "}" — que vive DENTRO del
    /// valor de cadena, no al cierre del objeto (D-5).
    /// </summary>
    private static bool TryExtractBalancedJson(string text, out string json)
    {
        json = string.Empty;

        int start = text.IndexOf('{', StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;

                if (depth == 0)
                {
                    json = text[start..(i + 1)];
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseJsonAnswer(string json, out LlmCategoryAnswer answer)
    {
        answer = Unparseable;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            JsonElement? value = FindCategoryProperty(root);
            if (value is null)
            {
                return false;
            }

            if (value.Value.ValueKind == JsonValueKind.Null)
            {
                answer = ExplicitNone;
                return true;
            }

            if (value.Value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string? rawValue = value.Value.GetString();
            string candidate = (rawValue ?? string.Empty).Trim();

            if (candidate.Length == 0)
            {
                return false;
            }

            if (IsNoneToken(candidate))
            {
                answer = ExplicitNone;
                return true;
            }

            answer = new LlmCategoryAnswer(LlmAnswerKind.Name, candidate);
            return true;
        }
    }

    /// <summary>
    /// Busca la primera propiedad presente entre <c>category</c>,
    /// <c>categoria</c>, <c>name</c> — en ese orden de prioridad,
    /// independiente del orden real de las claves en el JSON — comparando
    /// nombres con <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    private static JsonElement? FindCategoryProperty(JsonElement root)
    {
        foreach (string propertyName in _categoryPropertyNames)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Fallback de texto plano (D-5 paso 3): solo aplica si el paso 2 no
    /// encontró NADA de JSON parseable. Acepta el texto limpio como nombre
    /// SOLO si es de una sola línea (sin <c>\r</c>/<c>\n</c> tras recortar)
    /// y ≤ <see cref="MaxBareAnswerLength"/> tras quitar comillas
    /// envolventes y puntuación final. Un párrafo de prosa es
    /// <see cref="LlmAnswerKind.Unparseable"/>, no una categoría.
    /// </summary>
    private static bool TryBareString(string cleaned, out LlmCategoryAnswer answer)
    {
        answer = Unparseable;

        if (cleaned.Length == 0)
        {
            return false;
        }

        if (cleaned.Contains('\r', StringComparison.Ordinal) || cleaned.Contains('\n', StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = CleanBareCandidate(cleaned);

        if (candidate.Length == 0 || candidate.Length > MaxBareAnswerLength)
        {
            return false;
        }

        if (IsNoneToken(candidate))
        {
            answer = ExplicitNone;
            return true;
        }

        answer = new LlmCategoryAnswer(LlmAnswerKind.Name, candidate);
        return true;
    }

    private static string CleanBareCandidate(string value)
    {
        string trimmed = value.Trim();

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed.TrimEnd('.', ',', ';', ':', '!', '?');
    }

    private static bool IsNoneToken(string candidate) =>
        Array.IndexOf(_noneTokens, candidate.Trim().ToUpperInvariant()) >= 0;
}

/// <summary>
/// Resultado de <see cref="LlmCategoryAnswerParser.Parse"/> (F-013, D-5).
/// </summary>
/// <param name="Kind">Categoría del resultado del parseo.</param>
/// <param name="CategoryName">
/// Nombre de categoría candidato extraído, o <c>null</c> cuando
/// <paramref name="Kind"/> no es <see cref="LlmAnswerKind.Name"/>. El caso
/// de uso sigue teniendo que emparejarlo contra las categorías reales
/// (D-6) — este tipo no lo hace.
/// </param>
public sealed record LlmCategoryAnswer(LlmAnswerKind Kind, string? CategoryName);

/// <summary>Categoría del resultado de <see cref="LlmCategoryAnswerParser.Parse"/> (F-013, D-5).</summary>
public enum LlmAnswerKind
{
    /// <summary>Se extrajo un nombre candidato; el caso de uso aún tiene que emparejarlo.</summary>
    Name = 0,

    /// <summary>El modelo declinó deliberadamente: JSON <c>null</c>, o el literal <c>null</c>/<c>none</c>/<c>ninguna</c>.</summary>
    ExplicitNone = 1,

    /// <summary>Nada usable pudo extraerse.</summary>
    Unparseable = 2,
}
