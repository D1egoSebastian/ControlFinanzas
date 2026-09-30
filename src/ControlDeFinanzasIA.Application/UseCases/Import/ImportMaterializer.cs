using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Import;

/// <summary>
/// Turns an <see cref="ImportPlan"/> into domain entities BEFORE anything is written (F-020, DD-12,
/// Reconciliation #10). Every entity comes from a domain factory, so a row the domain refuses fails the whole
/// build: the caller either gets the complete <see cref="ImportBatch"/> or an exception and no batch at all.
/// Pure: no I/O, no clock (the instant is passed in), no ids from outside the plan.
/// </summary>
public static class ImportMaterializer
{
    /// <summary>Builds the batch for the valid rows of a plan.</summary>
    /// <param name="plan">The plan; only <see cref="ImportRowStatus.Valid"/> rows are materialized.</param>
    /// <param name="utcNow">The instant stamped on every created entity.</param>
    /// <returns>New categories (first-seen order), expenses and incomes.</returns>
    /// <exception cref="ArgumentException">The domain refused a row (defensive: the validator mirrors its invariants).</exception>
    /// <exception cref="InvalidOperationException">A valid row lacks a field or a category the plan should have resolved.</exception>
    public static ImportBatch Build(ImportPlan plan, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var newCategories = new List<Category>(plan.NewCategories.Count);
        var newCategoryIds = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (NewCategoryPlan category in plan.NewCategories)
        {
            var created = new Category
            {
                Id = Guid.NewGuid(),
                Name = category.Name,
                Color = ImportLimits.NewCategoryColor,
                Icon = null,
                IsSystem = false,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            };

            newCategories.Add(created);
            newCategoryIds[DuplicateDetector.NewCategoryKey(category.FoldKey)] = created.Id;
        }

        var expenses = new List<Expense>();
        var incomes = new List<Income>();

        foreach (PlannedRow row in plan.Rows)
        {
            if (row.Status != ImportRowStatus.Valid)
            {
                continue;
            }

            ParsedRow parsed = row.Parsed;

            if (parsed.Type is not TransactionType type
                || parsed.Date is not DateOnly date
                || parsed.Amount is not decimal amount
                || parsed.Currency is not Currency currency
                || parsed.Description is not string description)
            {
                throw new InvalidOperationException($"Row {parsed.RowNumber} is valid but has no date, type, amount, currency or description.");
            }

            Guid? categoryId = ResolveCategoryId(row, newCategoryIds);
            var money = Money.Create(amount, currency);

            if (type == TransactionType.Expense)
            {
                if (categoryId is not Guid expenseCategory)
                {
                    throw new InvalidOperationException($"Row {parsed.RowNumber} is an expense without a resolved category.");
                }

                expenses.Add(Expense.Create(expenseCategory, date, money, description, null, utcNow, row.PaymentMethodId));
            }
            else
            {
                incomes.Add(Income.Create(categoryId, date, money, description, null, null, utcNow, row.PaymentMethodId));
            }
        }

        return new ImportBatch(newCategories, expenses, incomes);
    }

    private static Guid? ResolveCategoryId(PlannedRow row, Dictionary<string, Guid> newCategoryIds)
    {
        if (!row.IsNewCategory)
        {
            return row.CategoryId;
        }

        if (!newCategoryIds.TryGetValue(row.CategoryKey, out Guid id))
        {
            throw new InvalidOperationException($"Row {row.Parsed.RowNumber} refers to a new category the plan does not list.");
        }

        return id;
    }
}
