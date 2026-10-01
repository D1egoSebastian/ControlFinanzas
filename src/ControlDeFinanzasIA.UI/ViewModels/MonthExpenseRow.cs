namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// A single row of the Dashboard's "expenses of the month" list.
/// </summary>
/// <param name="DateDisplay">The short, localized date (for example "12 Mar").</param>
/// <param name="Description">The expense description.</param>
/// <param name="CategoryName">The category name; empty when the category no longer exists.</param>
/// <param name="AmountDisplay">The exact amount with its own currency (for example "1,234.50 USD").</param>
/// <param name="DopDisplay">The "approximately DOP" equivalent for foreign-currency rows; empty for DOP rows and when no rate is available.</param>
public sealed record MonthExpenseRow(
    string DateDisplay,
    string Description,
    string CategoryName,
    string AmountDisplay,
    string DopDisplay);
