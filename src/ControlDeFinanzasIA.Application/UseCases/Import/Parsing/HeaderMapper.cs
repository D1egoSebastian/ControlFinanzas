using System.Globalization;
using System.Text;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

/// <summary>Result of automatic header mapping (F-020, DD-3).</summary>
/// <param name="Mapping">Columns matched to fields; a field with no or several candidates stays <c>null</c>.</param>
/// <param name="Missing">Required fields with no candidate column.</param>
/// <param name="Conflicting">Fields with several candidate columns; the user must pick.</param>
/// <param name="Ignored">Indexes of headers deliberately never mapped (such as the DOP-normalized amount).</param>
public sealed record HeaderMapping(
    ColumnMapping Mapping,
    IReadOnlyList<ImportField> Missing,
    IReadOnlyList<ImportField> Conflicting,
    IReadOnlyList<int> Ignored);

/// <summary>
/// Maps file headers to importable fields by exact equality against a synonym table (F-020, R-9, C-11).
/// Never substring or prefix matching, so <c>AmountDOP</c> can never land on <c>Amount</c>.
/// </summary>
public static class HeaderMapper
{
    private static readonly ImportField[] _fields = Enum.GetValues<ImportField>();

    private static readonly ImportField[] _required = { ImportField.Date, ImportField.Description, ImportField.Amount };

    private static readonly Dictionary<string, ImportField> _synonyms = BuildSynonyms();

    private static readonly HashSet<string> _ignoredHeaders = new(StringComparer.Ordinal)
    {
        NormalizeHeader("Amount DOP"),
        NormalizeHeader("Monto DOP"),
    };

    /// <summary>Maps the headers automatically.</summary>
    /// <param name="headers">Header texts, in column order.</param>
    public static HeaderMapping AutoMap(IReadOnlyList<string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var candidates = new Dictionary<ImportField, List<int>>();
        var ignored = new List<int>();

        for (int i = 0; i < headers.Count; i++)
        {
            string key = NormalizeHeader(headers[i]);

            if (_ignoredHeaders.Contains(key))
            {
                ignored.Add(i);
            }
            else if (_synonyms.TryGetValue(key, out ImportField field))
            {
                if (!candidates.TryGetValue(field, out List<int>? list))
                {
                    list = new List<int>();
                    candidates[field] = list;
                }

                list.Add(i);
            }
        }

        int? Pick(ImportField field) =>
            candidates.TryGetValue(field, out List<int>? list) && list.Count == 1 ? list[0] : null;

        var mapping = new ColumnMapping(
            Pick(ImportField.Date),
            Pick(ImportField.Type),
            Pick(ImportField.Description),
            Pick(ImportField.Category),
            Pick(ImportField.Amount),
            Pick(ImportField.Currency),
            Pick(ImportField.PaymentMethod));

        ImportField[] conflicting = _fields
            .Where(f => candidates.TryGetValue(f, out List<int>? list) && list.Count > 1)
            .ToArray();

        ImportField[] missing = _required
            .Where(f => !candidates.ContainsKey(f))
            .ToArray();

        return new HeaderMapping(mapping, missing, conflicting, ignored);
    }

    /// <summary>
    /// Trims, removes diacritics, upper-cases with the invariant culture and keeps only letters and digits.
    /// </summary>
    /// <param name="header">Raw header text.</param>
    public static string NormalizeHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);

        string decomposed = header.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, ImportField> BuildSynonyms()
    {
        var table = new Dictionary<string, ImportField>(StringComparer.Ordinal);

        void Add(ImportField field, params string[] keys)
        {
            foreach (string key in keys)
            {
                table.Add(NormalizeHeader(key), field);
            }
        }

        Add(ImportField.Date, "Date", "Fecha", "Transaction date", "Fecha operación", "Fecha transacción");
        Add(ImportField.Type, "Type", "Tipo", "Kind", "Tipo movimiento");
        Add(ImportField.Description, "Description", "Descripción", "Concepto", "Detalle", "Memo", "Details");
        Add(ImportField.Category, "Category", "Categoría");
        Add(ImportField.Amount, "Amount", "Monto", "Importe", "Valor", "Value");
        Add(ImportField.Currency, "Currency", "Moneda", "Divisa");
        Add(ImportField.PaymentMethod, "Payment method", "Método de pago", "Forma de pago", "Método pago");

        return table;
    }
}
