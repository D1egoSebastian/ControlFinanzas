using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Import;

/// <summary>
/// Pure planner (F-020, DD-7, DD-8, DD-11): resolves categories and payment methods, classifies duplicates by
/// occurrence counting against the stored rows and hashes the outcome. It performs no I/O and reads no clock,
/// so the same inputs always give the same plan; Preview and Commit rely on that.
/// </summary>
public static class ImportPlanner
{
    /// <summary>Plans validated rows against a database snapshot.</summary>
    /// <param name="parsedRows">The validated rows, in file order.</param>
    /// <param name="snapshot">Categories, payment methods and stored transactions of the file's date span.</param>
    /// <param name="options">The import options.</param>
    public static PlannedRows Plan(IReadOnlyList<ParsedRow> parsedRows, ImportLookupSnapshot snapshot, ImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(parsedRows);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);

        var resolver = new CategoryResolver(snapshot.Categories, snapshot.PaymentMethods, options.CreateMissingCategories);
        var resolved = new List<Resolved>(parsedRows.Count);
        var fingerprints = new List<string?>(parsedRows.Count);

        // Rows are resolved in file order and only when the validator accepted them, so a rejected row never
        // reserves a new category (or one of the 50 slots) and never consumes a duplicate occurrence.
        foreach (ParsedRow parsed in parsedRows)
        {
            Resolved row = ResolveRow(parsed, resolver);
            resolved.Add(row);
            fingerprints.Add(row.Fingerprint);
        }

        IReadOnlyDictionary<string, int> existingCounts = DuplicateDetector.CountExisting(snapshot.Existing);
        IReadOnlyList<DuplicateVerdict> verdicts = DuplicateDetector.Classify(fingerprints, existingCounts, options.SkipDuplicates);

        var rows = new List<PlannedRow>(resolved.Count);
        var signatures = new List<string>(resolved.Count);

        for (int i = 0; i < resolved.Count; i++)
        {
            Resolved row = resolved[i];
            List<ImportReason> reasons = row.Reasons;

            if (verdicts[i].AddsDuplicateImportedWarning)
            {
                reasons = new List<ImportReason>(reasons) { ImportReason.DuplicateImported };
            }

            rows.Add(new PlannedRow(
                row.Parsed,
                verdicts[i].Status,
                row.CategoryId,
                row.CategoryKey,
                row.CategoryName,
                row.IsNewCategory,
                row.PaymentMethodId,
                reasons));

            // The payment method id is not part of the duplicate fingerprint, but it is part of what gets written.
            signatures.Add(
                DuplicateDetector.RowSignature(row.Parsed.RowNumber, verdicts[i].Status, row.Fingerprint, reasons)
                + "|pm:" + (row.PaymentMethodId?.ToString("N") ?? string.Empty));
        }

        string hash = DuplicateDetector.ComputePlanHash(signatures, resolver.NewCategories.Select(c => c.Name));
        return new PlannedRows(rows, resolver.NewCategories.ToList(), hash);
    }

    /// <summary>The hash of a plan with no evaluated rows (an incomplete mapping).</summary>
    public static string EmptyPlanHash() => DuplicateDetector.ComputePlanHash(Array.Empty<string>(), Array.Empty<string>());

    private static Resolved ResolveRow(ParsedRow parsed, CategoryResolver resolver)
    {
        if (parsed.IsRejected)
        {
            return new Resolved(parsed, null, DuplicateDetector.CategoryKey(null), null, false, null, parsed.Reasons.ToList(), null);
        }

        if (parsed.Type is not TransactionType type
            || parsed.Date is not DateOnly date
            || parsed.Amount is not decimal amount
            || parsed.Currency is not Currency currency
            || parsed.Description is not string description)
        {
            throw new InvalidOperationException("The row validator accepted a row without a date, type, amount, currency or description.");
        }

        CategoryResolution category = resolver.ResolveCategory(type, parsed.CategoryText);
        var reasons = parsed.Reasons.Concat(category.Reasons).ToList();

        if (category.IsRejected)
        {
            return new Resolved(parsed, null, category.CategoryKey, category.Name, false, null, reasons, null);
        }

        PaymentMethodResolution payment = resolver.ResolvePaymentMethod(parsed.PaymentMethodText);
        reasons.AddRange(payment.Reasons);

        string fingerprint = DuplicateDetector.Fingerprint(date, type, amount, currency, description, category.CategoryKey);
        return new Resolved(parsed, category.CategoryId, category.CategoryKey, category.Name, category.IsNew, payment.PaymentMethodId, reasons, fingerprint);
    }

    private sealed record Resolved(
        ParsedRow Parsed,
        Guid? CategoryId,
        string CategoryKey,
        string? CategoryName,
        bool IsNewCategory,
        Guid? PaymentMethodId,
        List<ImportReason> Reasons,
        string? Fingerprint);
}
