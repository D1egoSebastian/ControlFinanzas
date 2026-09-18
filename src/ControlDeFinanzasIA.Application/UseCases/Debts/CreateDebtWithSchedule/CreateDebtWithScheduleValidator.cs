using ControlDeFinanzasIA.Domain.Debts;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Debts.CreateDebtWithSchedule;

/// <summary>
/// Validador FluentValidation para <see cref="CreateDebtWithScheduleCommand"/>.
/// Re-valida en el borde las invariantes de <see cref="Debt"/> (R-1,
/// R-2), devolviendo mensajes en espa&#241;ol consistentes con el resto
/// del proyecto.
/// </summary>
public sealed class CreateDebtWithScheduleValidator : AbstractValidator<CreateDebtWithScheduleCommand>
{
    /// <summary>
    /// Inicializa una nueva instancia del validador.
    /// </summary>
    public CreateDebtWithScheduleValidator()
    {
        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(Debt.MaxDescriptionLength)
            .WithMessage($"La descripción no puede superar {Debt.MaxDescriptionLength} caracteres.");

        RuleFor(c => c.OriginalAmount)
            .GreaterThan(0).WithMessage("El monto original debe ser mayor a 0.");

        RuleFor(c => c.TotalInstallments)
            .GreaterThan(0).WithMessage("El número de cuotas debe ser mayor a 0.");

        RuleFor(c => c.InstallmentAmount)
            .GreaterThan(0).WithMessage("El monto por cuota debe ser mayor a 0.");

        RuleFor(c => c.Frequency)
            .NotEmpty().WithMessage("La frecuencia es obligatoria.")
            .Must(f => f == nameof(DebtFrequency.Monthly) || f == nameof(DebtFrequency.Biweekly))
            .WithMessage("La frecuencia debe ser Monthly o Biweekly.");

        RuleFor(c => c.StartDate)
            .GreaterThanOrEqualTo(new DateOnly(1900, 1, 1))
            .WithMessage("La fecha de inicio debe ser >= 1900-01-01.");

        RuleFor(c => c.CounterpartyName)
            .MaximumLength(Debt.MaxCounterpartyNameLength)
            .WithMessage($"La contraparte no puede superar {Debt.MaxCounterpartyNameLength} caracteres.")
            .When(c => c.CounterpartyName is not null);

        RuleFor(c => c.Note)
            .MaximumLength(Debt.MaxNoteLength)
            .WithMessage($"La nota no puede superar {Debt.MaxNoteLength} caracteres.")
            .When(c => c.Note is not null);
    }
}
