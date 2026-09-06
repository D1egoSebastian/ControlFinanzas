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
    /// <param name="income">The income to map.</param>
    /// <returns>A new <see cref="IncomeDto"/> with all scalar fields copied.</returns>
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
            income.UpdatedAt);
}
