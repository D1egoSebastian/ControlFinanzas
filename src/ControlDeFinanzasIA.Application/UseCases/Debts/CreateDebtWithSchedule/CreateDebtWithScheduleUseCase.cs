using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Debts.CreateDebtWithSchedule;

/// <summary>
/// Resultado exitoso de <see cref="CreateDebtWithScheduleUseCase"/>:
/// la deuda creada junto con el calendario completo de cuotas que se
/// gener&#243; y persisti&#243; en la misma operaci&#243;n (R-3).
/// </summary>
public sealed record CreateDebtWithScheduleResult(Debt Debt, IReadOnlyList<DebtPayment> Payments);

/// <summary>
/// Use case que registra una nueva deuda: crea la entidad
/// (<see cref="Debt.Create"/>), genera su calendario completo
/// (<see cref="DebtScheduleGenerator"/>) y persiste ambos en una sola
/// operaci&#243;n transaccional (R-3, S-1, S-2).
/// </summary>
public sealed class CreateDebtWithScheduleUseCase
{
    private readonly IDebtRepository _repository;
    private readonly IValidator<CreateDebtWithScheduleCommand> _validator;
    private readonly ILogger<CreateDebtWithScheduleUseCase> _logger;
    private readonly DebtScheduleGenerator _generator = new();

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public CreateDebtWithScheduleUseCase(
        IDebtRepository repository,
        IValidator<CreateDebtWithScheduleCommand> validator,
        ILogger<CreateDebtWithScheduleUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el registro de la deuda y la generaci&#243;n de su calendario.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="CreateDebtWithScheduleResult"/>
    /// en <see cref="Result.Value"/> si la operaci&#243;n tiene &#233;xito;
    /// <see cref="Error"/> con c&#243;digo <c>"Validation"</c> o
    /// <c>"DomainInvariant"</c> en caso contrario, sin escribir ni la
    /// deuda ni sus cuotas (S-2).
    /// </returns>
    public async Task<Result> ExecuteAsync(CreateDebtWithScheduleCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        FluentValidation.Results.ValidationResult validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            IReadOnlyDictionary<string, string[]> details = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            return Result.Failure(
                "Validation",
                "El comando para registrar la deuda no es válido.",
                details);
        }

        Debt debt;
        IReadOnlyList<DebtPayment> payments;
        try
        {
            var originalAmount = Money.Create(command.OriginalAmount, command.Currency);
            var installmentAmount = Money.Create(command.InstallmentAmount, command.Currency);
            debt = Debt.Create(
                description: command.Description,
                originalAmount: originalAmount,
                currency: command.Currency,
                totalInstallments: command.TotalInstallments,
                installmentAmount: installmentAmount,
                frequency: command.Frequency,
                startDate: command.StartDate,
                counterpartyName: command.CounterpartyName,
                note: command.Note);

            payments = _generator.Generate(debt);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation while creating debt.");
            return Result.Failure("DomainInvariant", ex.Message);
        }

        Result addResult = await _repository.AddWithScheduleAsync(debt, payments, ct).ConfigureAwait(false);
        if (!addResult.IsSuccess)
        {
            return addResult;
        }

        return Result.Success(new CreateDebtWithScheduleResult(debt, payments));
    }
}
