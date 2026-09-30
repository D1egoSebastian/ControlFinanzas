using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

/// <summary>Planned status of one row after duplicate detection.</summary>
/// <param name="Status">Valid, Duplicate or Rejected.</param>
/// <param name="AddsDuplicateImportedWarning">The row is a duplicate that is imported anyway.</param>
public readonly record struct DuplicateVerdict(ImportRowStatus Status, bool AddsDuplicateImportedWarning);

/// <summary>
/// Duplicate detection and plan hashing (F-020, DD-8, R-17, C-17, C-18). A row is a duplicate only while
/// stored rows with the same fingerprint remain to absorb it (occurrence counting), so identical rows that
/// really repeat in the file are kept and a re-import inserts nothing.
/// </summary>
public static class DuplicateDetector
{
    private const char _separator = '\u001F';

    /// <summary>Builds the identity of a transaction.</summary>
    /// <param name="date">Transaction date.</param>
    /// <param name="type">Expense or income.</param>
    /// <param name="amount">The amount; only its value to four decimals matters.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="description">The description.</param>
    /// <param name="categoryKey">See <see cref="CategoryKey"/> and <see cref="NewCategoryKey"/>.</param>
    public static string Fingerprint(
        DateOnly date,
        TransactionType type,
        decimal amount,
        Currency currency,
        string description,
        string categoryKey)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(categoryKey);

        return string.Join(
            _separator,
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ((int)type).ToString(CultureInfo.InvariantCulture),
            decimal.Round(amount, ImportLimits.MaxFractionDigits, MidpointRounding.AwayFromZero).ToString("0.0000", CultureInfo.InvariantCulture),
            ((int)currency).ToString(CultureInfo.InvariantCulture),
            Escape(ImportText.DescriptionKey(description)),
            Escape(categoryKey));
    }

    /// <summary>The category component for an existing category (<c>N</c> format) or an empty string for none.</summary>
    /// <param name="categoryId">The category id.</param>
    public static string CategoryKey(Guid? categoryId) =>
        categoryId is Guid id ? id.ToString("N", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>The category component for a category the import will create.</summary>
    /// <param name="foldKey">The fold key of the new category.</param>
    public static string NewCategoryKey(string foldKey)
    {
        ArgumentNullException.ThrowIfNull(foldKey);
        return "new:" + foldKey;
    }

    /// <summary>Counts stored transactions per fingerprint, using the same fingerprint as the file side.</summary>
    /// <param name="existing">The stored transactions inside the file's date span.</param>
    public static IReadOnlyDictionary<string, int> CountExisting(IEnumerable<ExistingTransactionKey> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        Dictionary<string, int> counts = new(StringComparer.Ordinal);

        foreach (ExistingTransactionKey key in existing)
        {
            string fingerprint = Fingerprint(key.Date, key.Type, key.Amount, key.Currency, key.Description, CategoryKey(key.CategoryId));
            counts[fingerprint] = counts.GetValueOrDefault(fingerprint) + 1;
        }

        return counts;
    }

    /// <summary>
    /// Marks which rows are duplicates. A <c>null</c> fingerprint is a rejected row: it is never a duplicate and
    /// does not consume an occurrence.
    /// </summary>
    /// <param name="fileFingerprints">One entry per file row, in file order.</param>
    /// <param name="existingCounts">Stored rows per fingerprint.</param>
    public static IReadOnlyList<bool> ClassifyDuplicates(
        IReadOnlyList<string?> fileFingerprints,
        IReadOnlyDictionary<string, int> existingCounts)
    {
        ArgumentNullException.ThrowIfNull(fileFingerprints);
        ArgumentNullException.ThrowIfNull(existingCounts);

        Dictionary<string, int> seen = new(StringComparer.Ordinal);
        bool[] result = new bool[fileFingerprints.Count];

        for (int i = 0; i < fileFingerprints.Count; i++)
        {
            string? fingerprint = fileFingerprints[i];

            if (fingerprint is null)
            {
                continue;
            }

            int occurrence = seen.GetValueOrDefault(fingerprint) + 1;
            seen[fingerprint] = occurrence;
            result[i] = occurrence <= existingCounts.GetValueOrDefault(fingerprint);
        }

        return result;
    }

    /// <summary>Turns duplicate flags into row statuses according to the skip option.</summary>
    /// <param name="fileFingerprints">One entry per file row; <c>null</c> for rejected rows.</param>
    /// <param name="existingCounts">Stored rows per fingerprint.</param>
    /// <param name="skipDuplicates">Whether duplicates are skipped or imported with a warning.</param>
    public static IReadOnlyList<DuplicateVerdict> Classify(
        IReadOnlyList<string?> fileFingerprints,
        IReadOnlyDictionary<string, int> existingCounts,
        bool skipDuplicates)
    {
        IReadOnlyList<bool> duplicates = ClassifyDuplicates(fileFingerprints, existingCounts);
        var verdicts = new DuplicateVerdict[duplicates.Count];

        for (int i = 0; i < verdicts.Length; i++)
        {
            verdicts[i] = (fileFingerprints[i], duplicates[i], skipDuplicates) switch
            {
                (null, _, _) => new DuplicateVerdict(ImportRowStatus.Rejected, false),
                (_, true, true) => new DuplicateVerdict(ImportRowStatus.Duplicate, false),
                (_, true, false) => new DuplicateVerdict(ImportRowStatus.Valid, true),
                _ => new DuplicateVerdict(ImportRowStatus.Valid, false),
            };
        }

        return verdicts;
    }

    /// <summary>One line of the plan hash input: row number, status, fingerprint and reasons.</summary>
    /// <param name="rowNumber">Source row number.</param>
    /// <param name="status">Planned status.</param>
    /// <param name="fingerprint">The fingerprint, or <c>null</c> for rejected rows.</param>
    /// <param name="reasons">All reasons of the row.</param>
    public static string RowSignature(int rowNumber, ImportRowStatus status, string? fingerprint, IReadOnlyList<ImportReason> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        string reasonList = string.Join(',', reasons.Select(r => ((int)r).ToString(CultureInfo.InvariantCulture)));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{rowNumber}:{(int)status}:{fingerprint ?? string.Empty}|{reasonList}");
    }

    /// <summary>Lowercase hex SHA-256 over the ordered row signatures and the ordered new category names.</summary>
    /// <param name="rowSignatures">Output of <see cref="RowSignature"/> in file order.</param>
    /// <param name="newCategoryNames">Names of the categories the import will create, in order.</param>
    public static string ComputePlanHash(IEnumerable<string> rowSignatures, IEnumerable<string> newCategoryNames)
    {
        ArgumentNullException.ThrowIfNull(rowSignatures);
        ArgumentNullException.ThrowIfNull(newCategoryNames);

        StringBuilder builder = new();

        foreach (string signature in rowSignatures)
        {
            builder.Append("R:").Append(signature).Append('\n');
        }

        foreach (string name in newCategoryNames)
        {
            builder.Append("C:").Append(name).Append('\n');
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Reversible escape so the separator inside a component can never forge another fingerprint.</summary>
    private static string Escape(string component) =>
        component.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(_separator.ToString(), "\\s", StringComparison.Ordinal);
}
