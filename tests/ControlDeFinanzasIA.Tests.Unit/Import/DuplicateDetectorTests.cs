using System.Globalization;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Domain.Enums;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>Tests for <see cref="DuplicateDetector"/> (F-020, R-17, S-19, C-17, C-18).</summary>
public sealed class DuplicateDetectorTests
{
    private static readonly DateOnly _date = new(2026, 3, 4);
    private static readonly Guid _category = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    private static string Fp(
        decimal amount = 12.5m,
        string description = "Coffee",
        string? categoryKey = null,
        TransactionType type = TransactionType.Expense,
        Currency currency = Currency.DOP,
        DateOnly? date = null) =>
        DuplicateDetector.Fingerprint(date ?? _date, type, amount, currency, description, categoryKey ?? DuplicateDetector.CategoryKey(_category));

    // Fingerprint

    [Fact]
    public void Fingerprint_AmountScaleDoesNotMatter_12_5_12_50_12_5000()
    {
        string a = Fp(12.5m);
        string b = Fp(12.50m);
        string c = Fp(12.5000m);

        Assert.Equal(a, b);
        Assert.Equal(a, c);
        Assert.Contains("12.5000", a, StringComparison.Ordinal);
    }

    [Fact]
    public void Fingerprint_DifferentAmountsDiffer()
    {
        Assert.NotEqual(Fp(12.5m), Fp(12.5001m));
        Assert.NotEqual(Fp(1234.5m), Fp(12345m));
    }

    [Fact]
    public void Fingerprint_DescriptionDifferingOnlyByCaseOrWhitespaceRunsIsEqual()
    {
        Assert.Equal(Fp(description: "Coffee shop"), Fp(description: "  COFFEE   shop "));
        Assert.Equal(Fp(description: "Coffee shop"), Fp(description: "coffee\tshop"));
    }

    [Fact]
    public void Fingerprint_DescriptionDifferingByDiacriticsOrPunctuationIsNotEqual_Conservative()
    {
        Assert.NotEqual(Fp(description: "Cafe"), Fp(description: "Café"));
        Assert.NotEqual(Fp(description: "Cafe"), Fp(description: "Cafe."));
    }

    [Fact]
    public void Fingerprint_EveryComponentParticipates()
    {
        string baseline = Fp();

        Assert.NotEqual(baseline, Fp(date: _date.AddDays(1)));
        Assert.NotEqual(baseline, Fp(type: TransactionType.Income));
        Assert.NotEqual(baseline, Fp(currency: Currency.USD));
        Assert.NotEqual(baseline, Fp(categoryKey: DuplicateDetector.CategoryKey(Guid.Parse("00000000-0000-0000-0000-00000000000b"))));
    }

    [Fact]
    public void CategoryKey_Variants_GuidN_NewFold_AndEmptyForNull()
    {
        Assert.Equal(_category.ToString("N", CultureInfo.InvariantCulture), DuplicateDetector.CategoryKey(_category));
        Assert.Equal("new:EDUCACION", DuplicateDetector.NewCategoryKey("EDUCACION"));
        Assert.Equal(string.Empty, DuplicateDetector.CategoryKey(null));
    }

    [Fact]
    public void Fingerprint_ANewCategoryNeverEqualsAnExistingOne() => Assert.NotEqual(Fp(categoryKey: DuplicateDetector.NewCategoryKey("COMIDA")), Fp(categoryKey: DuplicateDetector.CategoryKey(_category)));

    [Fact]
    public void Fingerprint_SeparatorInsideTheDescriptionCannotForgeAnotherFingerprint()
    {
        // Without escaping both would serialize the tail as COFFEE|0|X.
        string forged = DuplicateDetector.Fingerprint(_date, TransactionType.Expense, 12.5m, Currency.DOP, "Coffee\u001F0", "X");
        string honest = DuplicateDetector.Fingerprint(_date, TransactionType.Expense, 12.5m, Currency.DOP, "Coffee", "0\u001FX");
        string backslash = DuplicateDetector.Fingerprint(_date, TransactionType.Expense, 12.5m, Currency.DOP, "Coffee\\s", "X");
        string separator = DuplicateDetector.Fingerprint(_date, TransactionType.Expense, 12.5m, Currency.DOP, "Coffee\u001F", "X");

        Assert.NotEqual(honest, forged);
        Assert.NotEqual(backslash, separator);
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    [InlineData("de-DE")]
    public void Fingerprint_IgnoresTheCurrentCulture(string cultureName)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            string expected = Fp(1234.5m, "Café i");
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            Assert.Equal(expected, Fp(1234.5m, "Café i"));
            Assert.Equal(expected.Length, Fp(1234.5m, "CAFÉ I").Length);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Fingerprint_TurkishCulture_DoesNotSplitTheLetterI()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal(Fp(description: "Internet"), Fp(description: "internet"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // Occurrence counting, C-17

    private static IReadOnlyDictionary<string, int> Existing(params (string Fingerprint, int Count)[] entries) =>
        entries.ToDictionary(e => e.Fingerprint, e => e.Count, StringComparer.Ordinal);

    [Fact]
    public void ClassifyDuplicates_TwoIdenticalRowsAndNoneStored_BothImported_C17()
    {
        string fp = Fp();

        IReadOnlyList<bool> result = DuplicateDetector.ClassifyDuplicates(new string?[] { fp, fp }, Existing());

        Assert.Equal(new[] { false, false }, result);
    }

    [Fact]
    public void ClassifyDuplicates_OneStored_FirstIsDuplicateSecondImported_S19()
    {
        string fp = Fp();

        IReadOnlyList<bool> result = DuplicateDetector.ClassifyDuplicates(new string?[] { fp, fp }, Existing((fp, 1)));

        Assert.Equal(new[] { true, false }, result);
    }

    [Fact]
    public void ClassifyDuplicates_TwoStored_BothDuplicates()
    {
        string fp = Fp();

        IReadOnlyList<bool> result = DuplicateDetector.ClassifyDuplicates(new string?[] { fp, fp }, Existing((fp, 2)));

        Assert.Equal(new[] { true, true }, result);
    }

    [Fact]
    public void ClassifyDuplicates_ReImport_EveryRowIsADuplicate_C18()
    {
        string a = Fp(1m);
        string b = Fp(2m);
        string[] file = { a, b, a, a, b };

        IReadOnlyList<bool> result = DuplicateDetector.ClassifyDuplicates(file, Existing((a, 3), (b, 2)));

        Assert.All(result, isDuplicate => Assert.True(isDuplicate));
        Assert.Equal(file.Length, result.Count);
    }

    [Fact]
    public void ClassifyDuplicates_MoreStoredThanInTheFile_StillAllDuplicates()
    {
        string fp = Fp();

        Assert.Equal(new[] { true }, DuplicateDetector.ClassifyDuplicates(new string?[] { fp }, Existing((fp, 5))));
    }

    [Fact]
    public void ClassifyDuplicates_RejectedRowsDoNotConsumeOccurrences()
    {
        string fp = Fp();

        // Row 0 was rejected (null): it must not take the single stored occurrence away from row 1.
        IReadOnlyList<bool> result = DuplicateDetector.ClassifyDuplicates(new string?[] { null, fp, fp }, Existing((fp, 1)));

        Assert.Equal(new[] { false, true, false }, result);
    }

    [Fact]
    public void ClassifyDuplicates_DifferentFingerprintsAreCountedIndependently()
    {
        string a = Fp(1m);
        string b = Fp(2m);

        IReadOnlyList<bool> result = DuplicateDetector.ClassifyDuplicates(new string?[] { a, b, b, a }, Existing((a, 1)));

        Assert.Equal(new[] { true, false, false, false }, result);
    }

    [Fact]
    public void ClassifyDuplicates_EmptyFile_IsEmpty() =>
        Assert.Empty(DuplicateDetector.ClassifyDuplicates(Array.Empty<string?>(), Existing()));

    [Fact]
    public void CountExisting_GroupsStoredRowsByTheSameFingerprintTheFileSideUses()
    {
        ExistingTransactionKey[] stored =
        {
            new(_date, TransactionType.Expense, 12.5m, Currency.DOP, "Coffee", _category),
            new(_date, TransactionType.Expense, 12.5000m, Currency.DOP, "  COFFEE ", _category),
            new(_date, TransactionType.Income, 12.5m, Currency.DOP, "Coffee", null),
        };

        IReadOnlyDictionary<string, int> counts = DuplicateDetector.CountExisting(stored);

        Assert.Equal(2, counts[Fp()]);
        Assert.Equal(1, counts[Fp(type: TransactionType.Income, categoryKey: string.Empty)]);
        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void CountExisting_ThenClassify_TheTwoSidesShareTheFourDecimalDecimal()
    {
        // SQLite hands back "12.5", an XLSX double round trips as 12.50: both must count as the same row.
        ExistingTransactionKey[] stored = { new(_date, TransactionType.Expense, 12.5m, Currency.DOP, "Coffee", _category) };

        IReadOnlyList<bool> result = DuplicateDetector.ClassifyDuplicates(new string?[] { Fp(12.5000m) }, DuplicateDetector.CountExisting(stored));

        Assert.Equal(new[] { true }, result);
    }

    // SkipDuplicates

    [Fact]
    public void Classify_SkipOn_DuplicatesAreDuplicateStatus_RejectedStayRejected()
    {
        string fp = Fp();

        IReadOnlyList<DuplicateVerdict> verdicts = DuplicateDetector.Classify(new string?[] { fp, null, fp }, Existing((fp, 1)), skipDuplicates: true);

        Assert.Equal(new DuplicateVerdict(ImportRowStatus.Duplicate, false), verdicts[0]);
        Assert.Equal(new DuplicateVerdict(ImportRowStatus.Rejected, false), verdicts[1]);
        Assert.Equal(new DuplicateVerdict(ImportRowStatus.Valid, false), verdicts[2]);
    }

    [Fact]
    public void Classify_SkipOff_DuplicatesBecomeValidWithADuplicateImportedWarning()
    {
        string fp = Fp();

        IReadOnlyList<DuplicateVerdict> verdicts = DuplicateDetector.Classify(new string?[] { fp, fp }, Existing((fp, 2)), skipDuplicates: false);

        Assert.All(verdicts, v => Assert.Equal(new DuplicateVerdict(ImportRowStatus.Valid, true), v));
    }

    [Fact]
    public void Classify_SkipOff_UniqueRowsCarryNoWarning()
    {
        string fp = Fp();

        IReadOnlyList<DuplicateVerdict> verdicts = DuplicateDetector.Classify(new string?[] { fp }, Existing(), skipDuplicates: false);

        Assert.Equal(new DuplicateVerdict(ImportRowStatus.Valid, false), verdicts[0]);
    }

    // Plan hash

    [Fact]
    public void PlanHash_IsStableLowercaseSha256Hex()
    {
        string[] rows = { DuplicateDetector.RowSignature(2, ImportRowStatus.Valid, Fp(), new[] { ImportReason.FutureDate }) };
        string hash = DuplicateDetector.ComputePlanHash(rows, new[] { "Viajes" });

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Equal(hash, DuplicateDetector.ComputePlanHash(rows, new[] { "Viajes" }));
    }

    [Fact]
    public void PlanHash_ChangesWhenAnyPlannedFactChanges()
    {
        string fp = Fp();
        string baseline = DuplicateDetector.ComputePlanHash(
            new[] { DuplicateDetector.RowSignature(2, ImportRowStatus.Valid, fp, Array.Empty<ImportReason>()) }, new[] { "Viajes" });

        string status = DuplicateDetector.ComputePlanHash(
            new[] { DuplicateDetector.RowSignature(2, ImportRowStatus.Duplicate, fp, Array.Empty<ImportReason>()) }, new[] { "Viajes" });
        string row = DuplicateDetector.ComputePlanHash(
            new[] { DuplicateDetector.RowSignature(3, ImportRowStatus.Valid, fp, Array.Empty<ImportReason>()) }, new[] { "Viajes" });
        string reason = DuplicateDetector.ComputePlanHash(
            new[] { DuplicateDetector.RowSignature(2, ImportRowStatus.Valid, fp, new[] { ImportReason.CategoryFallback }) }, new[] { "Viajes" });
        string print = DuplicateDetector.ComputePlanHash(
            new[] { DuplicateDetector.RowSignature(2, ImportRowStatus.Valid, Fp(13m), Array.Empty<ImportReason>()) }, new[] { "Viajes" });
        string category = DuplicateDetector.ComputePlanHash(
            new[] { DuplicateDetector.RowSignature(2, ImportRowStatus.Valid, fp, Array.Empty<ImportReason>()) }, new[] { "Otro" });
        string noCategory = DuplicateDetector.ComputePlanHash(
            new[] { DuplicateDetector.RowSignature(2, ImportRowStatus.Valid, fp, Array.Empty<ImportReason>()) }, Array.Empty<string>());

        Assert.Equal(6, new[] { status, row, reason, print, category, noCategory }.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(baseline, new[] { status, row, reason, print, category, noCategory });
    }

    [Fact]
    public void PlanHash_RowOrderMattersAndCategoryOrderMatters()
    {
        string a = DuplicateDetector.RowSignature(2, ImportRowStatus.Valid, Fp(1m), Array.Empty<ImportReason>());
        string b = DuplicateDetector.RowSignature(3, ImportRowStatus.Valid, Fp(2m), Array.Empty<ImportReason>());

        Assert.NotEqual(DuplicateDetector.ComputePlanHash(new[] { a, b }, Array.Empty<string>()), DuplicateDetector.ComputePlanHash(new[] { b, a }, Array.Empty<string>()));
        Assert.NotEqual(DuplicateDetector.ComputePlanHash(Array.Empty<string>(), new[] { "A", "B" }), DuplicateDetector.ComputePlanHash(Array.Empty<string>(), new[] { "B", "A" }));
    }

    [Fact]
    public void PlanHash_RowsAndCategoriesCannotBeConfusedForEachOther()
    {
        Assert.NotEqual(
            DuplicateDetector.ComputePlanHash(new[] { "X" }, Array.Empty<string>()),
            DuplicateDetector.ComputePlanHash(Array.Empty<string>(), new[] { "X" }));
    }

    [Fact]
    public void RowSignature_RejectedRowWithNoFingerprint_IsStillDistinctPerReason()
    {
        string a = DuplicateDetector.RowSignature(2, ImportRowStatus.Rejected, null, new[] { ImportReason.InvalidDate });
        string b = DuplicateDetector.RowSignature(2, ImportRowStatus.Rejected, null, new[] { ImportReason.MissingDate });

        Assert.NotEqual(a, b);
    }
}
