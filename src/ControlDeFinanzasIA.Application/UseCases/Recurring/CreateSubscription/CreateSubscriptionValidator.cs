using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;

/// <summary>
/// Validador FluentValidation para <see cref="CreateSubscriptionCommand"/>.
/// Re-valida las invariantes de <see cref="RecurringRule"/> en el
/// borde (antes de llamar al factory), devolviendo mensajes en
/// espanol consistentes con el resto del proyecto.
/// </summary>
public sealed class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionCommand>
{
    /// <summary>
    /// Inicializa una nueva instancia del validador.
    /// </summary>
    public CreateSubscriptionValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres.");

        RuleFor(c => c.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a 0.");

        RuleFor(c => c.Frequency)
            .NotEmpty().WithMessage("La frecuencia es obligatoria.")
            .Must(f => f == "Monthly" || f == "Bimonthly" || f == "Yearly")
            .WithMessage("La frecuencia debe ser Monthly, Bimonthly o Yearly.");

        RuleFor(c => c.StartDate)
            .GreaterThanOrEqualTo(new DateOnly(1900, 1, 1))
            .WithMessage("La fecha de inicio debe ser >= 1900-01-01.");

        RuleFor(c => c.EndDate)
            .GreaterThanOrEqualTo(c => c.StartDate)
            .When(c => c.EndDate.HasValue)
            .WithMessage("La fecha de fin debe ser >= fecha de inicio.");

        RuleFor(c => c.CategoryId)
            .NotEqual(Guid.Empty)
            .WithMessage("La categoria es obligatoria.");

        RuleFor(c => c.PaymentMethodId)
            .NotEqual(Guid.Empty)
            .WithMessage("El metodo de pago es obligatorio.");
    }
}
