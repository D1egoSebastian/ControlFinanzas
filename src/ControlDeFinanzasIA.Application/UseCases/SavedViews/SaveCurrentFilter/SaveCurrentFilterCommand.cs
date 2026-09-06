using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;

/// <summary>
/// Immutable command to save the current filter as a named saved view.
/// The audit timestamps are sourced from <c>IClock</c> by the use case,
/// never supplied by the caller.
/// </summary>
public sealed record SaveCurrentFilterCommand(
    /// <summary>
    /// Descriptive name of the saved view.
    /// </summary>
    string Name,

    /// <summary>
    /// Entity type the saved view applies to (Expense or Income).
    /// </summary>
    TransactionType EntityType,

    /// <summary>
    /// Current filter instance (an <c>ExpenseFilter</c> or <c>IncomeFilter</c>).
    /// </summary>
    object Filter);
