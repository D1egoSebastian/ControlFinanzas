using ControlDeFinanzasIA.Application.DTOs;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;

/// <summary>
/// Wrapper returned on a successful <see cref="RegisterExpenseUseCase.ExecuteAsync"/> call.
/// It contains the created expense DTO and an immutable list of warnings.
/// </summary>
public sealed record RegisterExpenseResult(
    ExpenseDto Expense,
    IReadOnlyList<string> Warnings);
