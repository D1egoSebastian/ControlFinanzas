using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Transactions;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>Already-built domain entities handed to the writer (F-020, DD-1).</summary>
/// <param name="NewCategories">Categories to create.</param>
/// <param name="Expenses">Expenses to insert.</param>
/// <param name="Incomes">Incomes to insert.</param>
public sealed record ImportBatch(
    IReadOnlyList<Category> NewCategories,
    IReadOnlyList<Expense> Expenses,
    IReadOnlyList<Income> Incomes);
