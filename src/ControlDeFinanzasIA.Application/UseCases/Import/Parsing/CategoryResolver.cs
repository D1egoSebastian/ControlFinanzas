using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Parsing;

/// <summary>Outcome of resolving the category of one row (F-020, DD-7).</summary>
/// <param name="CategoryId">The existing category id; <c>null</c> for a category to be created or for no category.</param>
/// <param name="Name">The category name shown in the preview.</param>
/// <param name="IsNew">The category will be created by the import.</param>
/// <param name="CategoryKey">The category component of the duplicate fingerprint.</param>
/// <param name="Reasons">Rejections and warnings.</param>
public sealed record CategoryResolution(
    Guid? CategoryId,
    string? Name,
    bool IsNew,
    string CategoryKey,
    IReadOnlyList<ImportReason> Reasons)
{
    /// <summary>Gets a value indicating whether any reason rejects the row.</summary>
    public bool IsRejected => Reasons.Any(ImportReasons.IsRejection);
}

/// <summary>Outcome of resolving the payment method of one row.</summary>
/// <param name="PaymentMethodId">The payment method id, or <c>null</c>.</param>
/// <param name="Reasons">Warnings.</param>
public sealed record PaymentMethodResolution(Guid? PaymentMethodId, IReadOnlyList<ImportReason> Reasons);

/// <summary>
/// Stateful, per-batch category and payment-method resolver (F-020, DD-7, R-14..R-16). Rows must be resolved
/// in file order and only when the validator did not already reject them, so a rejected row never reserves a
/// new category. The instance keeps the new categories seen so far: duplicate spellings create one category
/// (first spelling wins) and at most <see cref="ImportLimits.MaxNewCategories"/> are created.
/// </summary>
public sealed class CategoryResolver
{
    private readonly IReadOnlyList<CatalogEntry> _categories;
    private readonly IReadOnlyList<CatalogEntry> _paymentMethods;
    private readonly bool _createMissing;
    private readonly Dictionary<string, CatalogMatch> _categoryCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogMatch> _paymentCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NewCategoryPlan> _newByKey = new(StringComparer.Ordinal);
    private readonly List<NewCategoryPlan> _newInOrder = new();

    /// <summary>Initializes a new instance of the <see cref="CategoryResolver"/> class.</summary>
    /// <param name="categories">Existing categories.</param>
    /// <param name="paymentMethods">Existing payment methods.</param>
    /// <param name="createMissing">Whether unknown categories may be created.</param>
    public CategoryResolver(
        IReadOnlyList<CatalogEntry> categories,
        IReadOnlyList<CatalogEntry> paymentMethods,
        bool createMissing)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(paymentMethods);

        _categories = categories;
        _paymentMethods = paymentMethods;
        _createMissing = createMissing;
    }

    /// <summary>Gets the categories the import will create, in first-seen order.</summary>
    public IReadOnlyList<NewCategoryPlan> NewCategories => _newInOrder;

    /// <summary>Resolves the category of one non-rejected row.</summary>
    /// <param name="type">The row type.</param>
    /// <param name="text">The category cell, or <c>null</c> when unmapped.</param>
    public CategoryResolution ResolveCategory(TransactionType type, string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return type == TransactionType.Expense ? Fallback() : NoCategory(Array.Empty<ImportReason>());
        }

        switch (MatchCategory(trimmed))
        {
            case CatalogMatch.Found found:
                return new CategoryResolution(found.Id, found.Name, false, DuplicateDetector.CategoryKey(found.Id), Array.Empty<ImportReason>());
            case CatalogMatch.Ambiguous:
                return Rejected(ImportReason.AmbiguousCategory);
            default:
                return ResolveUnknown(type, trimmed);
        }
    }

    /// <summary>Resolves the payment method of one row. Payment methods are matched, never created.</summary>
    /// <param name="text">The payment method cell, or <c>null</c> when unmapped.</param>
    public PaymentMethodResolution ResolvePaymentMethod(string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return new PaymentMethodResolution(null, Array.Empty<ImportReason>());
        }

        if (!_paymentCache.TryGetValue(trimmed, out CatalogMatch? match))
        {
            match = CategoryMatcher.Match(trimmed, _paymentMethods);
            _paymentCache[trimmed] = match;
        }

        return match is CatalogMatch.Found found
            ? new PaymentMethodResolution(found.Id, Array.Empty<ImportReason>())
            : new PaymentMethodResolution(null, new[] { ImportReason.PaymentMethodNotFound });
    }

    private static CategoryResolution NoCategory(IReadOnlyList<ImportReason> reasons) =>
        new(null, null, false, DuplicateDetector.CategoryKey(null), reasons);

    private static CategoryResolution Rejected(ImportReason reason) =>
        new(null, null, false, DuplicateDetector.CategoryKey(null), new[] { reason });

    private CatalogMatch MatchCategory(string trimmed)
    {
        if (!_categoryCache.TryGetValue(trimmed, out CatalogMatch? match))
        {
            match = CategoryMatcher.Match(trimmed, _categories);
            _categoryCache[trimmed] = match;
        }

        return match;
    }

    private CategoryResolution ResolveUnknown(TransactionType type, string trimmed)
    {
        string key = CategoryMatcher.FoldKey(trimmed);

        if (!_createMissing || key.Length == 0)
        {
            return type == TransactionType.Expense ? Fallback() : NoCategory(new[] { ImportReason.CategoryDropped });
        }

        string name = ImportText.CollapseWhitespace(trimmed);

        if (name.Length > ImportLimits.MaxCategoryNameChars)
        {
            return Rejected(ImportReason.CategoryNameTooLong);
        }

        if (!_newByKey.TryGetValue(key, out NewCategoryPlan? plan))
        {
            if (_newInOrder.Count >= ImportLimits.MaxNewCategories)
            {
                return Rejected(ImportReason.TooManyNewCategories);
            }

            plan = new NewCategoryPlan(key, name);
            _newByKey[key] = plan;
            _newInOrder.Add(plan);
        }

        return new CategoryResolution(null, plan.Name, true, DuplicateDetector.NewCategoryKey(plan.FoldKey), Array.Empty<ImportReason>());
    }

    private CategoryResolution Fallback()
    {
        CatalogEntry? fallback = _categories.FirstOrDefault(c => c.Id == ImportLimits.FallbackCategoryId);

        if (fallback is null)
        {
            return Rejected(ImportReason.NoFallbackCategory);
        }

        return new CategoryResolution(
            fallback.Id,
            fallback.Name,
            false,
            DuplicateDetector.CategoryKey(fallback.Id),
            new[] { ImportReason.CategoryFallback });
    }
}
