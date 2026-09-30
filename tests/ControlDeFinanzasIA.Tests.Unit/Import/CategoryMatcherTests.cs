using System.Globalization;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Domain.Enums;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>Tests for <see cref="CategoryMatcher"/>, <see cref="CategoryResolver"/> and <see cref="ImportText"/> (F-020, R-14..R-16, S-15..S-18, C-14, C-15).</summary>
public sealed class CategoryMatcherTests
{
    private static readonly Guid _comida = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid _alimentacion = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid _educacionUpper = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid _educacionAccent = Guid.Parse("00000000-0000-0000-0000-00000000000d");

    private static CatalogEntry Entry(Guid id, string name) => new(id, name, false);

    private static readonly CatalogEntry _otros = new(ImportLimits.FallbackCategoryId, "Otros", true);

    private static IReadOnlyList<CatalogEntry> Catalog() => new[]
    {
        Entry(_comida, "Comida"),
        Entry(_alimentacion, "Alimentación"),
        _otros,
    };

    // Matcher tiers

    [Fact]
    public void Match_IgnoreCase_ComidaMatchesLowercase()
    {
        CatalogMatch match = CategoryMatcher.Match("comida", Catalog());

        Assert.Equal(new CatalogMatch.Found(_comida, "Comida"), match);
    }

    [Fact]
    public void Match_Folded_AlimentacionMatchesTheAccentedName_S15()
    {
        CatalogMatch match = CategoryMatcher.Match("alimentacion", Catalog());

        Assert.Equal(new CatalogMatch.Found(_alimentacion, "Alimentación"), match);
    }

    [Fact]
    public void Match_FoldedFromAnAccentedInputToAnUnaccentedName()
    {
        CatalogMatch match = CategoryMatcher.Match("COMIDÁ", Catalog());

        Assert.Equal(new CatalogMatch.Found(_comida, "Comida"), match);
    }

    [Fact]
    public void Match_TwoEntriesFoldingToTheSameKey_IsAmbiguous_S15_C14()
    {
        IReadOnlyList<CatalogEntry> catalog = new[]
        {
            Entry(_educacionAccent, "Educación"),
            Entry(_educacionUpper, "EDUCACION"),
        };

        Assert.IsType<CatalogMatch.Ambiguous>(CategoryMatcher.Match("educacion", catalog));
    }

    [Fact]
    public void Match_OrdinalExactWinsEvenWhenAFoldedTwinExists_C14()
    {
        IReadOnlyList<CatalogEntry> catalog = new[]
        {
            Entry(_educacionAccent, "Educación"),
            Entry(_educacionUpper, "EDUCACION"),
        };

        Assert.Equal(new CatalogMatch.Found(_educacionUpper, "EDUCACION"), CategoryMatcher.Match("EDUCACION", catalog));
        Assert.Equal(new CatalogMatch.Found(_educacionAccent, "Educación"), CategoryMatcher.Match("Educación", catalog));
    }

    [Fact]
    public void Match_IgnoreCaseAloneNeverDecidesWhileAFoldedTwinExists_C14()
    {
        IReadOnlyList<CatalogEntry> catalog = new[]
        {
            Entry(_educacionAccent, "Educación"),
            Entry(_educacionUpper, "EDUCACION"),
        };

        // "Educacion" ignore-case equals only "EDUCACION", but "Educación" is just as plausible: refuse to guess.
        Assert.IsType<CatalogMatch.Ambiguous>(CategoryMatcher.Match("Educacion", catalog));
    }

    [Fact]
    public void Match_TwoOrdinalIgnoreCaseCandidates_IsAmbiguous_C14()
    {
        IReadOnlyList<CatalogEntry> catalog = new[]
        {
            Entry(_comida, "Comida"),
            Entry(_alimentacion, "COMIDA"),
        };

        Assert.IsType<CatalogMatch.Ambiguous>(CategoryMatcher.Match("comida", catalog));
    }

    [Theory]
    [InlineData("Viajes")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void Match_NoCandidate_IsNotFound(string text) => Assert.IsType<CatalogMatch.NotFound>(CategoryMatcher.Match(text, Catalog()));

    [Fact]
    public void Match_TextWithPunctuationOnly_NeverMatchesAnEmptyFold()
    {
        IReadOnlyList<CatalogEntry> catalog = new[] { Entry(_comida, "!!!") };

        Assert.IsType<CatalogMatch.NotFound>(CategoryMatcher.Match("???", catalog));
    }

    [Fact]
    public void FoldKey_IsTheSharedNormalizer_NotAReimplementation()
    {
        foreach (string text in new[] { "Alimentación", "  \"comida\".  ", "EDUCACIÓN", "a   b", "Ñandú" })
        {
            Assert.Equal(CategoryNameNormalizer.Fold(text), CategoryMatcher.FoldKey(text));
        }
    }

    [Fact]
    public void Match_IgnoresTheCurrentCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            foreach (string name in new[] { "tr-TR", "es-DO", "en" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                IReadOnlyList<CatalogEntry> catalog = new[] { Entry(_comida, "Ingresos") };

                Assert.Equal(new CatalogMatch.Found(_comida, "Ingresos"), CategoryMatcher.Match("INGRESOS", catalog));
                Assert.Equal(new CatalogMatch.Found(_comida, "Ingresos"), CategoryMatcher.Match("ingresos", catalog));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // NOCASE vs FoldKey (R-14)

    [Fact]
    public void FoldKey_EverySqliteNocaseEqualPairShareAFoldKey()
    {
        SQLitePCL.Batteries_V2.Init();
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();

        using (SqliteCommand create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE t (name TEXT COLLATE NOCASE)";
            create.ExecuteNonQuery();
        }

        string[] samples =
        {
            "Comida", "COMIDA", "comida", "cOmIdA", "Alimentación", "ALIMENTACIóN", "alimentación", "Café", "CAFé",
            "a b", "A B", "A  B", "Otros", "OTROS", "Ñandu", "ñandu", "Niño", "NIñO", "x.", "X.", "\"Q\"", "\"q\"",
            "straße", "STRAßE", "I", "i", "İ", "ı",
        };

        int pairsChecked = 0;
        foreach (string a in samples)
        {
            foreach (string b in samples)
            {
                using SqliteCommand compare = connection.CreateCommand();
                compare.CommandText = "SELECT $a = $b COLLATE NOCASE";
                compare.Parameters.AddWithValue("$a", a);
                compare.Parameters.AddWithValue("$b", b);
                bool nocaseEqual = Convert.ToInt64(compare.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;

                if (nocaseEqual)
                {
                    pairsChecked++;
                    Assert.Equal(CategoryMatcher.FoldKey(a), CategoryMatcher.FoldKey(b));
                }
            }
        }

        Assert.True(pairsChecked > samples.Length);
    }

    [Fact]
    public void FoldKey_AsciiCaseVariantsOfRandomNamesAlwaysShareAKey()
    {
        Random random = new(20260930);
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ áéíóúñ.-";

        for (int i = 0; i < 500; i++)
        {
            string name = new(Enumerable.Range(0, random.Next(1, 20)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            string flipped = new(name.Select(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' && random.Next(2) == 0 ? (char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c)) : c).ToArray());

            Assert.Equal(CategoryMatcher.FoldKey(name), CategoryMatcher.FoldKey(flipped));
        }
    }

    // ImportText

    [Theory]
    [InlineData("  a   b  ", "a b")]
    [InlineData("a\t\nb", "a b")]
    [InlineData("a  b", "a b")]
    [InlineData("a\u0085b", "a b")]
    [InlineData("a  b", "a b")]
    [InlineData("", "")]
    public void ImportText_CollapseWhitespace_TrimsAndCollapsesEveryKindOfSpace(string input, string expected) => Assert.Equal(expected, ImportText.CollapseWhitespace(input));

    [Fact]
    public void ImportText_DescriptionKey_IgnoresCaseAndWhitespaceRunsButKeepsDiacritics()
    {
        Assert.Equal(ImportText.DescriptionKey("Café  con   leche"), ImportText.DescriptionKey("  CAFÉ con leche "));
        Assert.NotEqual(ImportText.DescriptionKey("Cafe"), ImportText.DescriptionKey("Café"));
        Assert.NotEqual(ImportText.DescriptionKey("Cafe."), ImportText.DescriptionKey("Cafe"));
    }

    [Theory]
    [InlineData("Íngreso", "INGRESO")]
    [InlineData("  gasto ", "GASTO")]
    [InlineData("Gasto.", "GASTO.")]
    public void ImportText_LooseKey_StripsDiacriticsAndUppercasesWithoutTouchingPunctuation(string input, string expected) => Assert.Equal(expected, ImportText.LooseKey(input));

    // Resolver, DD-7 table

    private static CategoryResolver Resolver(bool create = true, IReadOnlyList<CatalogEntry>? categories = null, IReadOnlyList<CatalogEntry>? methods = null) =>
        new(categories ?? Catalog(), methods ?? new[] { Entry(Guid.Parse("00000000-0000-0000-0000-0000000000f1"), "Efectivo") }, create);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ExpenseWithNoCategory_UsesTheFallbackByIdWithAWarning_D19(string? text)
    {
        CategoryResolution result = Resolver().ResolveCategory(TransactionType.Expense, text);

        Assert.Equal(ImportLimits.FallbackCategoryId, result.CategoryId);
        Assert.Equal("Otros", result.Name);
        Assert.False(result.IsNew);
        Assert.Equal(new[] { ImportReason.CategoryFallback }, result.Reasons);
        Assert.False(result.IsRejected);
    }

    [Fact]
    public void Resolve_ExpenseFallbackMissingFromTheCatalog_IsNoFallbackCategory()
    {
        CategoryResolver resolver = Resolver(categories: new[] { Entry(_comida, "Comida") });

        CategoryResolution result = resolver.ResolveCategory(TransactionType.Expense, null);

        Assert.True(result.IsRejected);
        Assert.Contains(ImportReason.NoFallbackCategory, result.Reasons);
        Assert.Null(result.CategoryId);
    }

    [Fact]
    public void Resolve_ExpenseFallbackIsFoundById_NotByTheNameOtros()
    {
        CatalogEntry renamed = new(ImportLimits.FallbackCategoryId, "Miscelánea", true);
        CatalogEntry impostor = Entry(Guid.NewGuid(), "Otros");
        CategoryResolver resolver = Resolver(categories: new[] { impostor, renamed });

        CategoryResolution result = resolver.ResolveCategory(TransactionType.Expense, string.Empty);

        Assert.Equal(ImportLimits.FallbackCategoryId, result.CategoryId);
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    public void Resolve_Found_ReturnsTheExistingId(TransactionType type)
    {
        CategoryResolution result = Resolver().ResolveCategory(type, "comida");

        Assert.Equal(_comida, result.CategoryId);
        Assert.Equal("Comida", result.Name);
        Assert.False(result.IsNew);
        Assert.Empty(result.Reasons);
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    public void Resolve_Ambiguous_RejectsTheRow_S15_C14(TransactionType type)
    {
        CategoryResolver resolver = Resolver(categories: new[] { Entry(_educacionAccent, "Educación"), Entry(_educacionUpper, "EDUCACION"), _otros });

        CategoryResolution result = resolver.ResolveCategory(type, "educacion");

        Assert.True(result.IsRejected);
        Assert.Contains(ImportReason.AmbiguousCategory, result.Reasons);
        Assert.Null(result.CategoryId);
        Assert.Empty(resolver.NewCategories);
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    public void Resolve_NotFoundWithCreationOn_PlansANewCategoryKeyedByFoldWithCollapsedWhitespace(TransactionType type)
    {
        CategoryResolver resolver = Resolver();

        CategoryResolution result = resolver.ResolveCategory(type, "  Gastos   de   Viaje ");

        Assert.True(result.IsNew);
        Assert.Null(result.CategoryId);
        Assert.Equal("Gastos de Viaje", result.Name);
        Assert.Equal("new:GASTOS DE VIAJE", result.CategoryKey);
        Assert.Empty(result.Reasons);
        NewCategoryPlan plan = Assert.Single(resolver.NewCategories);
        Assert.Equal("GASTOS DE VIAJE", plan.FoldKey);
        Assert.Equal("Gastos de Viaje", plan.Name);
    }

    [Fact]
    public void Resolve_NameOf100Chars_IsCreated_And101IsRejectedNeverTruncated_C15()
    {
        CategoryResolver resolver = Resolver();

        CategoryResolution ok = resolver.ResolveCategory(TransactionType.Expense, new string('a', 100));
        CategoryResolution tooLong = resolver.ResolveCategory(TransactionType.Expense, new string('b', 101));

        Assert.True(ok.IsNew);
        Assert.Equal(100, ok.Name!.Length);
        Assert.True(tooLong.IsRejected);
        Assert.Contains(ImportReason.CategoryNameTooLong, tooLong.Reasons);
        Assert.Null(tooLong.Name);
        Assert.Single(resolver.NewCategories);
    }

    [Fact]
    public void Resolve_The51stDistinctNewKey_IsRejected_AndTheFirst50AreCreated_R15()
    {
        CategoryResolver resolver = Resolver();

        for (int i = 0; i < ImportLimits.MaxNewCategories; i++)
        {
            Assert.True(resolver.ResolveCategory(TransactionType.Expense, "Nueva " + i.ToString(CultureInfo.InvariantCulture)).IsNew);
        }

        CategoryResolution over = resolver.ResolveCategory(TransactionType.Expense, "Nueva 50");
        CategoryResolution again = resolver.ResolveCategory(TransactionType.Expense, "nueva 0");

        Assert.True(over.IsRejected);
        Assert.Contains(ImportReason.TooManyNewCategories, over.Reasons);
        Assert.Equal(ImportLimits.MaxNewCategories, resolver.NewCategories.Count);
        Assert.True(again.IsNew);
        Assert.False(again.IsRejected);
    }

    [Fact]
    public void Resolve_DuplicateSpellingsInOneBatch_CreateOneCategory_FirstSpellingWins()
    {
        CategoryResolver resolver = Resolver();

        CategoryResolution first = resolver.ResolveCategory(TransactionType.Expense, "Niños");
        CategoryResolution second = resolver.ResolveCategory(TransactionType.Income, "NINOS");
        CategoryResolution third = resolver.ResolveCategory(TransactionType.Expense, "  niños ");

        NewCategoryPlan plan = Assert.Single(resolver.NewCategories);
        Assert.Equal("Niños", plan.Name);
        Assert.Equal("Niños", first.Name);
        Assert.Equal("Niños", second.Name);
        Assert.Equal("Niños", third.Name);
        Assert.Equal(first.CategoryKey, second.CategoryKey);
        Assert.Equal(first.CategoryKey, third.CategoryKey);
    }

    [Fact]
    public void Resolve_NewNamesNeverCollideWithTheCatalog_UnderNocaseOrFold_R14()
    {
        CategoryResolver resolver = Resolver();

        Assert.False(resolver.ResolveCategory(TransactionType.Expense, "COMIDA").IsNew);
        Assert.False(resolver.ResolveCategory(TransactionType.Expense, "  alimentacion. ").IsNew);
        Assert.Empty(resolver.NewCategories);
    }

    [Fact]
    public void Resolve_ExpenseNotFoundWithCreationOff_UsesTheFallbackWithAWarning()
    {
        CategoryResolver resolver = Resolver(create: false);

        CategoryResolution result = resolver.ResolveCategory(TransactionType.Expense, "Viajes");

        Assert.Equal(ImportLimits.FallbackCategoryId, result.CategoryId);
        Assert.Equal(new[] { ImportReason.CategoryFallback }, result.Reasons);
        Assert.Empty(resolver.NewCategories);
    }

    [Fact]
    public void Resolve_ExpenseNotFoundWithCreationOffAndNoFallback_IsRejected()
    {
        CategoryResolver resolver = Resolver(create: false, categories: new[] { Entry(_comida, "Comida") });

        CategoryResolution result = resolver.ResolveCategory(TransactionType.Expense, "Viajes");

        Assert.Contains(ImportReason.NoFallbackCategory, result.Reasons);
        Assert.True(result.IsRejected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_IncomeWithNoCategory_IsNullWithNoWarning_S17(string? text)
    {
        CategoryResolution result = Resolver().ResolveCategory(TransactionType.Income, text);

        Assert.Null(result.CategoryId);
        Assert.Null(result.Name);
        Assert.False(result.IsNew);
        Assert.Equal(string.Empty, result.CategoryKey);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Resolve_IncomeNotFoundWithCreationOff_IsNullWithACategoryDroppedWarning()
    {
        CategoryResolution result = Resolver(create: false).ResolveCategory(TransactionType.Income, "Viajes");

        Assert.Null(result.CategoryId);
        Assert.Equal(new[] { ImportReason.CategoryDropped }, result.Reasons);
        Assert.False(result.IsRejected);
        Assert.Equal(string.Empty, result.CategoryKey);
    }

    [Theory]
    [InlineData(TransactionType.Expense, ImportReason.CategoryFallback)]
    [InlineData(TransactionType.Income, ImportReason.CategoryDropped)]
    public void Resolve_CategoryThatFoldsToNothing_NeverCreatesAnEmptyKey(TransactionType type, ImportReason warning)
    {
        CategoryResolver resolver = Resolver();

        CategoryResolution result = resolver.ResolveCategory(type, "...");

        Assert.Empty(resolver.NewCategories);
        Assert.False(result.IsNew);
        Assert.Contains(warning, result.Reasons);
    }

    [Fact]
    public void Resolve_CategoryKeyForExistingCategory_IsTheGuidInNFormat()
    {
        CategoryResolution result = Resolver().ResolveCategory(TransactionType.Expense, "Comida");

        Assert.Equal(_comida.ToString("N", CultureInfo.InvariantCulture), result.CategoryKey);
    }

    // Payment method, S-18

    [Fact]
    public void ResolvePaymentMethod_FoundEmptyNotFoundAndAmbiguous_NeverCreates()
    {
        var efectivo = Guid.Parse("00000000-0000-0000-0000-0000000000f1");
        var tarjeta1 = Guid.Parse("00000000-0000-0000-0000-0000000000f2");
        var tarjeta2 = Guid.Parse("00000000-0000-0000-0000-0000000000f3");
        CategoryResolver resolver = Resolver(methods: new[] { Entry(efectivo, "Efectivo"), Entry(tarjeta1, "Tarjeta"), Entry(tarjeta2, "TARJETA") });

        PaymentMethodResolution found = resolver.ResolvePaymentMethod("efectivo");
        PaymentMethodResolution empty = resolver.ResolvePaymentMethod("  ");
        PaymentMethodResolution none = resolver.ResolvePaymentMethod(null);
        PaymentMethodResolution notFound = resolver.ResolvePaymentMethod("Cheque");
        PaymentMethodResolution ambiguous = resolver.ResolvePaymentMethod("tarjeta");

        Assert.Equal(efectivo, found.PaymentMethodId);
        Assert.Empty(found.Reasons);
        Assert.Null(empty.PaymentMethodId);
        Assert.Empty(empty.Reasons);
        Assert.Null(none.PaymentMethodId);
        Assert.Empty(none.Reasons);
        Assert.Null(notFound.PaymentMethodId);
        Assert.Equal(new[] { ImportReason.PaymentMethodNotFound }, notFound.Reasons);
        Assert.Null(ambiguous.PaymentMethodId);
        Assert.Equal(new[] { ImportReason.PaymentMethodNotFound }, ambiguous.Reasons);
        Assert.Empty(resolver.NewCategories);
    }
}
