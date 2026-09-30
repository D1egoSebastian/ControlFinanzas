using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

/// <summary>Outcome of matching a name against a catalog.</summary>
public abstract record CatalogMatch
{
    private CatalogMatch()
    {
    }

    /// <summary>Exactly one entry matched.</summary>
    /// <param name="Id">The entry id.</param>
    /// <param name="Name">The entry name.</param>
    public sealed record Found(Guid Id, string Name) : CatalogMatch;

    /// <summary>No entry matched.</summary>
    public sealed record NotFound : CatalogMatch;

    /// <summary>Several entries matched in the deciding tier, so no entry may be picked.</summary>
    public sealed record Ambiguous : CatalogMatch;
}

/// <summary>
/// Matches imported names against categories or payment methods (F-020, DD-7, R-14). An ordinal exact match
/// wins; otherwise every entry equal ignoring case or equal after the case- and diacritics-insensitive fold is
/// a candidate. Exactly one candidate is a match, several are <see cref="CatalogMatch.Ambiguous"/>, never a
/// guess (C-14).
/// </summary>
public static class CategoryMatcher
{
    /// <summary>The comparison key of a name: the shared category-name fold, not a second implementation.</summary>
    /// <param name="text">The raw name.</param>
    public static string FoldKey(string text) => CategoryNameNormalizer.Fold(text);

    /// <summary>Finds the catalog entry a name refers to.</summary>
    /// <param name="text">The imported name.</param>
    /// <param name="catalog">The catalog.</param>
    public static CatalogMatch Match(string text, IReadOnlyList<CatalogEntry> catalog)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(catalog);

        string trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return new CatalogMatch.NotFound();
        }

        CatalogMatch? exact = Decide(catalog.Where(e => string.Equals(e.Name, trimmed, StringComparison.Ordinal)).ToList());

        if (exact is not null)
        {
            return exact;
        }

        // Case-insensitive equality implies fold equality, so one candidate set covers both looser tiers. Deciding
        // on the ignore-case subset alone would silently pick "EDUCACION" for "educacion" while "Educación" also
        // exists; every looser candidate must therefore be counted together (C-14).
        string key = FoldKey(trimmed);
        var looser = catalog
            .Where(e => string.Equals(e.Name, trimmed, StringComparison.OrdinalIgnoreCase)
                || (key.Length > 0 && string.Equals(FoldKey(e.Name), key, StringComparison.Ordinal)))
            .ToList();

        return Decide(looser) ?? new CatalogMatch.NotFound();
    }

    private static CatalogMatch? Decide(IReadOnlyList<CatalogEntry> candidates) => candidates.Count switch
    {
        0 => null,
        1 => new CatalogMatch.Found(candidates[0].Id, candidates[0].Name),
        _ => new CatalogMatch.Ambiguous(),
    };
}
