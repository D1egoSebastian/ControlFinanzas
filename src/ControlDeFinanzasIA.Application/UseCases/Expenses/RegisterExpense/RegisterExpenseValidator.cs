using ControlDeFinanzasIA.Domain.Enums;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;

/// <summary>
/// Validates a <see cref="RegisterExpenseCommand"/> using synchronous
/// FluentValidation rules. Async validators are intentionally avoided
/// to prevent <c>AsyncValidatorInvokedSynchronouslyException</c>.
/// </summary>
public sealed class RegisterExpenseValidator : AbstractValidator<RegisterExpenseCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterExpenseValidator"/> class.
    /// </summary>
    public RegisterExpenseValidator()
    {
        RuleFor(c => c.CategoryId)
            .NotEqual(Guid.Empty).WithMessage("La categoría es obligatoria.");

        RuleFor(c => c.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor que 0.");

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(200).WithMessage("La descripción no puede superar 200 caracteres.")
            .Must(d => d.Trim().Length > 0).WithMessage("La descripción no puede contener solo espacios en blanco.");

        RuleFor(c => c.Note)
            .MaximumLength(500).When(c => c.Note is not null).WithMessage("La nota no puede superar 500 caracteres.");

        RuleFor(c => c.Currency)
            .IsInEnum().WithMessage("La moneda no es válida.");

        RuleFor(c => c.PaymentMethodId)
            .Must(id => id == null || id != Guid.Empty)
            .WithMessage("El método de pago no es válido.");
    }
}
