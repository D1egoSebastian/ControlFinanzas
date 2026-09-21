using ControlDeFinanzasIA.Domain.Goals;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.CreateGoal;

/// <summary>
/// Validador FluentValidation para <see cref="CreateGoalCommand"/>.
/// Re-valida en el borde las invariantes de <see cref="Goal"/> (R-2).
/// Deliberadamente NO valida <see cref="CreateGoalCommand.TargetDate"/>:
/// una fecha objetivo en el pasado es un input legal (Q2), no un error
/// de validación.
/// </summary>
public sealed class CreateGoalValidator : AbstractValidator<CreateGoalCommand>
{
    /// <summary>
    /// Inicializa una nueva instancia del validador.
    /// </summary>
    public CreateGoalValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("El nombre de la meta es requerido.")
            .MaximumLength(Goal.MaxNameLength)
                .WithMessage($"El nombre no puede superar los {Goal.MaxNameLength} caracteres.");

        RuleFor(c => c.TargetAmount)
            .GreaterThan(0m).WithMessage("El monto objetivo debe ser mayor a 0.");

        RuleFor(c => c.Note)
            .MaximumLength(Goal.MaxNoteLength)
                .WithMessage($"La nota no puede superar los {Goal.MaxNoteLength} caracteres.");

        // NO rule for TargetDate: a past target date is legal input (Q2).
    }
}
