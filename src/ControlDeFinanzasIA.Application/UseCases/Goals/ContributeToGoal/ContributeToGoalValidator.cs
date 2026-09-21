using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.ContributeToGoal;

/// <summary>
/// Validador FluentValidation para <see cref="ContributeToGoalCommand"/>.
/// La moneda NO se valida aquí: el chequeo de coincidencia contra la
/// moneda de la meta (R-4) requiere cargar la meta primero y vive en
/// <see cref="ContributeToGoalUseCase"/>, no en este validador de forma.
/// </summary>
public sealed class ContributeToGoalValidator : AbstractValidator<ContributeToGoalCommand>
{
    /// <summary>
    /// Inicializa una nueva instancia del validador.
    /// </summary>
    public ContributeToGoalValidator()
    {
        RuleFor(c => c.GoalId)
            .NotEqual(Guid.Empty).WithMessage("El identificador de la meta es requerido.");

        RuleFor(c => c.Amount)
            .GreaterThan(0m).WithMessage("El monto del aporte debe ser mayor a 0.");
    }
}
