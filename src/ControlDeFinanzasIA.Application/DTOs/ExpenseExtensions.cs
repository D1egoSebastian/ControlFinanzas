using ControlDeFinanzasIA.Domain.Transactions;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Mapping helpers between <see cref="Expense"/> and <see cref="ExpenseDto"/>.
/// </summary>
public static class ExpenseExtensions
{
    /// <summary>
    /// Maps a domain <see cref="Expense"/> to its application DTO.
    /// </summary>
    /// <param name="expense">The expense to map.</param>
    /// <returns>A new <see cref="ExpenseDto"/> with all scalar fields copied.</returns>
    public static ExpenseDto ToDto(this Expense expense)
        => new(
            expense.Id,
            expense.CategoryId,
            expense.Date,
            expense.Amount.Amount,
            expense.Amount.Currency,
            expense.Description,
            expense.Note,
            expense.CreatedAt,
            expense.UpdatedAt);
}
