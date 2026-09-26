using System.Text.RegularExpressions;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.GenerateSavingsPlan;

/// <summary>
/// Guard determinista y puro (sin DI) para la narracion de plan de ahorro
/// generada por IA (F-016, R-8, R-10, D-4). <see cref="Clean"/> normaliza la
/// salida cruda del modelo; <see cref="IsGrounded"/> verifica que ningun
/// numero de la narracion final sea distinto de los numeros realmente
/// enviados (las cifras del estado mas el nombre saneado de la meta,
/// Reconciliation #1). No conoce <c>Goal</c>, <c>SavingsPlan</c> ni
/// <c>ILLMClient</c> — solo cadenas.
/// </summary>
public static class SavingsPlanNarrationGuard
{
    /// <summary>Longitud maxima de la narracion limpiada (R-8, D-4).</summary>
    public const int MaxChars = 600;

    private const string _fenceMarker = "```";

    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly Regex _threeOrMoreNewlines = new(
        pattern: "\n{3,}",
        options: RegexOptions.None,
        matchTimeout: _regexTimeout);

    /// <summary>
    /// Normaliza la salida cruda del modelo: nunca devuelve <see
    /// langword="null"/>. Quita bloques de cerca (lineas que empiezan con
    /// ```` ``` ````), colapsa 3+ saltos de linea consecutivos a 2, recorta
    /// espacios en los extremos y, si el resultado supera
    /// <see cref="MaxChars"/>, corta en el ultimo espacio en blanco antes del
    /// limite para nunca partir una cifra a la mitad (R-8, D-4).
    /// </summary>
    public static string Clean(string? raw)
    {
        if (raw is null)
        {
            return string.Empty;
        }

        string normalized = raw
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        string withoutFences = RemoveFenceLines(normalized);
        string collapsed = _threeOrMoreNewlines.Replace(withoutFences, "\n\n");
        string trimmed = collapsed.Trim();

        return trimmed.Length <= MaxChars ? trimmed : TruncateAtWhitespace(trimmed);
    }

    /// <summary>
    /// <see langword="true"/> cuando cada tramo de digitos ASCII presente en
    /// <paramref name="narration"/> tambien aparece entre los tramos de
    /// digitos de <paramref name="suppliedValues"/> (las cifras enviadas al
    /// modelo para ese estado, mas el nombre saneado de la meta). Los ceros a
    /// la izquierda se ignoran al comparar (<c>05</c> equivale a <c>5</c>).
    /// Cualquier caracter numerico no-ASCII (digitos de ancho completo,
    /// superindices, fracciones) rechaza la narracion de inmediato (Spec
    /// Reconciliation #2, #3). Es una verificacion de pertenencia, no de
    /// posicion (limitacion documentada y aceptada).
    /// </summary>
    public static bool IsGrounded(string narration, IEnumerable<string> suppliedValues)
    {
        ArgumentNullException.ThrowIfNull(narration);
        ArgumentNullException.ThrowIfNull(suppliedValues);

        foreach (char c in narration)
        {
            if (char.IsNumber(c) && c is not (>= '0' and <= '9'))
            {
                return false;
            }
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in suppliedValues)
        {
            foreach (string run in AsciiDigitRuns(value))
            {
                allowed.Add(Normalize(run));
            }
        }

        foreach (string run in AsciiDigitRuns(narration))
        {
            if (!allowed.Contains(Normalize(run)))
            {
                return false;
            }
        }

        return true;
    }

    private static string RemoveFenceLines(string text)
    {
        string[] lines = text.Split('\n');
        var kept = new List<string>(lines.Length);

        foreach (string line in lines)
        {
            if (!line.TrimStart().StartsWith(_fenceMarker, StringComparison.Ordinal))
            {
                kept.Add(line);
            }
        }

        return string.Join('\n', kept);
    }

    private static string TruncateAtWhitespace(string trimmed)
    {
        string cut = trimmed[..(MaxChars - 1)];

        int lastWhitespace = -1;
        for (int i = cut.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(cut[i]))
            {
                lastWhitespace = i;
                break;
            }
        }

        if (lastWhitespace >= 0)
        {
            cut = cut[..lastWhitespace];
        }

        return cut.TrimEnd() + "…";
    }

    private static List<string> AsciiDigitRuns(string text)
    {
        var runs = new List<string>();
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] is >= '0' and <= '9')
            {
                int start = i;
                while (i < text.Length && text[i] is >= '0' and <= '9')
                {
                    i++;
                }

                runs.Add(text[start..i]);
            }
            else
            {
                i++;
            }
        }

        return runs;
    }

    private static string Normalize(string run)
    {
        string trimmed = run.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }
}
