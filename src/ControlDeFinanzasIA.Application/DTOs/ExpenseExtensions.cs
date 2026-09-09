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
    /// <remarks>
    /// <para>
    /// <see cref="ExpenseDto.PaymentMethodName"/> se popula a partir de
    /// <see cref="Expense.PaymentMethod"/> (la navegacion cargada via
    /// Include en el repositorio). Si el repositorio no hizo Include,
    /// el nombre sera <see langword="null"/> aunque <see cref="Expense.PaymentMethodId"/>
    /// este setteado. Esto es consistente con vision §4.2 (defensivo:
    /// un metodo borrado deja el nombre null pero conserva el FK).
    /// </para>
    /// </remarks>
    /// <param name="expense">The expense to map.</param>
    /// <returns>A new <see cref="ExpenseDto"/> with all fields copied.</returns>
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
            expense.UpdatedAt,
            expense.PaymentMethodId,
            expense.PaymentMethod?.Name);
}
