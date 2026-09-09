using ControlDeFinanzasIA.Domain.Transactions;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Mapping helpers between <see cref="Income"/> and <see cref="IncomeDto"/>.
/// </summary>
public static class IncomeExtensions
{
    /// <summary>
    /// Maps a domain <see cref="Income"/> to its application DTO.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IncomeDto.PaymentMethodName"/> se popula a partir de
    /// <see cref="Income.PaymentMethod"/> (la navegacion cargada via
    /// Include en el repositorio). Si el repositorio no hizo Include,
    /// el nombre sera <see langword="null"/> aunque <see cref="Income.PaymentMethodId"/>
    /// este setteado.
    /// </para>
    /// </remarks>
    /// <param name="income">The income to map.</param>
    /// <returns>A new <see cref="IncomeDto"/> with all fields copied.</returns>
    public static IncomeDto ToDto(this Income income)
        => new(
            income.Id,
            income.CategoryId,
            income.Date,
            income.Amount.Amount,
            income.Amount.Currency,
            income.Description,
            income.Note,
            income.Source,
            income.CreatedAt,
            income.UpdatedAt,
            income.PaymentMethodId,
            income.PaymentMethod?.Name,
            ConvertedAmountDisplay: null);
}
