using System.Text.Json;
using System.Text.RegularExpressions;

namespace ControlDeFinanzasIA.Application.Llm;

/// <summary>
/// Parser tolerante de la respuesta cruda por lotes de un <c>ILLMClient</c>
/// para la narración de anomalías (F-014, slice 5, D-9). Vive junto a
/// <see cref="LlmCategoryAnswerParser"/> y comparte su estilo (estático,
/// <see cref="RegexOptions.None"/> + timeout de 500 ms, sin conocimiento de
/// dominio) pero es un tipo independiente — R-9 prohíbe tocar el parser de
/// la Fase 13, así que el escáner balanceado se duplica deliberadamente
/// (~40 líneas) en vez de extraerse a un tipo compartido.
/// </summary>
public static class AnomalyNarrationParser
{
    /// <summary>Longitud máxima de un mensaje individual tras el parseo (D-9 paso 5).</summary>
    public const int MaxMessageLength = 280;

    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly Regex _fenceRegex = new(
        pattern: "^```(?:json)?\\s*(?<body>[\\s\\S]*?)\\s*```$",
        options: RegexOptions.None,
        matchTimeout: _regexTimeout);

    private static readonly IReadOnlyDictionary<int, string> _empty =
        new Dictionary<int, string>();

    /// <summary>
    /// Parsea la respuesta cruda del modelo en un diccionario ordinal →
    /// mensaje. Nunca lanza; nunca devuelve <see langword="null"/> — vacío
    /// cuando no hay nada usable. Un lote parcialmente malformado es el caso
    /// normal (spec S-6): los ids no resueltos simplemente están ausentes del
    /// diccionario devuelto, y el llamador conserva su plantilla
    /// determinística para esos.
    /// </summary>
    /// <param name="rawContent">Contenido crudo devuelto por el proveedor. Puede ser <c>null</c>.</param>
    /// <param name="expectedCount">
    /// Cantidad de anomalías enviadas en el lote. Los ids fuera del rango
    /// <c>[1, expectedCount]</c> se ignoran como "id desconocido" (D-9).
    /// </param>
    public static IReadOnlyDictionary<int, string> Parse(string? rawContent, int expectedCount)
    {
        if (string.IsNullOrWhiteSpace(rawContent) || expectedCount <= 0)
        {
            return _empty;
        }

        string cleaned = StripFence(rawContent.Trim());

        if (!TryExtractBalancedJson(cleaned, out string jsonCandidate))
        {
            return _empty;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonCandidate);
        }
        catch (JsonException)
        {
            return _empty;
        }

        using (document)
        {
            if (!TryGetItemsArray(document.RootElement, out JsonElement itemsArray))
            {
                return _empty;
            }

            return ParseItems(itemsArray, expectedCount);
        }
    }

    private static string StripFence(string trimmed)
    {
        Match match = _fenceRegex.Match(trimmed);
        return match.Success ? match.Groups["body"].Value.Trim() : trimmed;
    }

    /// <summary>
    /// Escanea la primera región JSON balanceada empezando en el primer
    /// <c>[</c> o <c>{</c> (lo que venga primero), con un contador de
    /// profundidad consciente de literales de cadena y secuencias de escape
    /// que cuenta ambos tipos de corchete a la vez — igual que
    /// <see cref="LlmCategoryAnswerParser"/> pero sin restringirse a
    /// objetos (D-9).
    /// </summary>
    private static bool TryExtractBalancedJson(string text, out string json)
    {
        json = string.Empty;

        int startBrace = text.IndexOf('{', StringComparison.Ordinal);
        int startBracket = text.IndexOf('[', StringComparison.Ordinal);

        int start;
        if (startBrace < 0 && startBracket < 0)
        {
            return false;
        }
        else if (startBrace < 0)
        {
            start = startBracket;
        }
        else if (startBracket < 0)
        {
            start = startBrace;
        }
        else
        {
            start = Math.Min(startBrace, startBracket);
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
            else if (c is '{' or '[')
            {
                depth++;
            }
            else if (c is '}' or ']')
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

    private static bool TryGetItemsArray(JsonElement root, out JsonElement itemsArray)
    {
        itemsArray = default;

        if (root.ValueKind == JsonValueKind.Array)
        {
            itemsArray = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            TryFindProperty(root, "items", out JsonElement itemsProperty) &&
            itemsProperty.ValueKind == JsonValueKind.Array)
        {
            itemsArray = itemsProperty;
            return true;
        }

        return false;
    }

    private static bool TryFindProperty(JsonElement root, string propertyName, out JsonElement value)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IReadOnlyDictionary<int, string> ParseItems(JsonElement itemsArray, int expectedCount)
    {
        var result = new Dictionary<int, string>();

        foreach (JsonElement item in itemsArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryGetId(item, out int id) || id < 1 || id > expectedCount)
            {
                continue;
            }

            // Primera ocurrencia gana (D-9 paso 5): un id repetido después no reemplaza al primero.
            if (result.ContainsKey(id))
            {
                continue;
            }

            if (!TryGetMessage(item, out string message))
            {
                continue;
            }

            result[id] = CapMessage(message);
        }

        return result;
    }

    private static bool TryGetId(JsonElement item, out int id)
    {
        id = 0;

        if (!TryFindProperty(item, "id", out JsonElement idElement))
        {
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.Number)
        {
            return idElement.TryGetInt32(out id);
        }

        if (idElement.ValueKind == JsonValueKind.String)
        {
            string? raw = idElement.GetString();
            return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out id);
        }

        return false;
    }

    private static bool TryGetMessage(JsonElement item, out string message)
    {
        message = string.Empty;

        if (!TryFindProperty(item, "message", out JsonElement messageElement) ||
            messageElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string candidate = (messageElement.GetString() ?? string.Empty).Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        message = candidate;
        return true;
    }

    private static string CapMessage(string message) =>
        message.Length > MaxMessageLength
            ? string.Concat(message.AsSpan(0, MaxMessageLength - 1), "…")
            : message;
}
