namespace ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;

/// <summary>
/// Wrapper returned on a successful
/// <see cref="RegisterIncomeUseCase.ExecuteAsync"/> call. It carries the
/// generated income id (not a full DTO — <c>IncomeDto</c> is scope of
/// F-008) and an immutable list of warnings.
/// </summary>
/// <remarks>
/// <see cref="Warnings"/> is never null: it is an empty list when no
/// warnings apply. A future date produces a <c>"FutureDateWarning"</c>
/// entry; the income is still accepted.
/// </remarks>
public sealed record RegisterIncomeResult(
    Guid IncomeId,
    IReadOnlyList<string> Warnings);
