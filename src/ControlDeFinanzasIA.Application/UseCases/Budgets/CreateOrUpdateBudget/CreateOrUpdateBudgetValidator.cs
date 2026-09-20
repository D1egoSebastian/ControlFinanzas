using ControlDeFinanzasIA.Domain.Budgets;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Budgets.CreateOrUpdateBudget;

/// <summary>
/// Validador FluentValidation para <see cref="CreateOrUpdateBudgetCommand"/>.
/// Re-valida en el borde las invariantes de <see cref="Budget"/> (R-3)
/// y rechaza explícitamente los presupuestos globales (R-8, S-9), ya
/// que <see cref="Budget.Create"/> exige un <see cref="Guid"/> no nulo.
/// </summary>
public sealed class CreateOrUpdateBudgetValidator : AbstractValidator<CreateOrUpdateBudgetCommand>
{
    /// <summary>
    /// Inicializa una nueva instancia del validador.
    /// </summary>
    public CreateOrUpdateBudgetValidator()
    {
        RuleFor(c => c.Year)
            .InclusiveBetween(Budget.MinYear, Budget.MaxYear)
            .WithMessage($"El año debe estar entre {Budget.MinYear} y {Budget.MaxYear}.");

        RuleFor(c => c.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("El mes debe estar entre 1 y 12.");

        // Nota: NotEqual(Guid.Empty) NO puede colgarse del mismo RuleFor que
        // NotNull() con un .When() al final — en FluentValidation, .When()
        // encadenado al final de un RuleFor gatea TODAS las reglas previas de
        // esa cadena, incluyendo NotNull(), así que un CategoryId nulo
        // saltaría silenciosamente la validación entera. Reglas separadas.
        RuleFor(c => c.CategoryId)
            .NotNull()
            .WithMessage("Los presupuestos globales no están soportados todavía.");

        RuleFor(c => c.CategoryId!.Value)
            .NotEqual(Guid.Empty)
            .WithMessage("Los presupuestos globales no están soportados todavía.")
            .When(c => c.CategoryId is not null);

        RuleFor(c => c.Amount)
            .GreaterThan(0).WithMessage("El monto del presupuesto debe ser mayor a 0.");
    }
}
