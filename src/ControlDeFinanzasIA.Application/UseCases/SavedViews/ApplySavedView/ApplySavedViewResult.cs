using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;

/// <summary>
/// Result of applying a saved view: the stored entity type plus the
/// deserialized, typed filter (an <c>ExpenseFilter</c> or <c>IncomeFilter</c>
/// instance boxed as <c>object</c>).
/// </summary>
public sealed record ApplySavedViewResult(
    /// <summary>
    /// Entity type the saved view applies to.
    /// </summary>
    TransactionType EntityType,

    /// <summary>
    /// Deserialized filter instance.
    /// </summary>
    object Filter);
