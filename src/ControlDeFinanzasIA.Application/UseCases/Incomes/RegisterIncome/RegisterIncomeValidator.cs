using ControlDeFinanzasIA.Domain.Enums;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;

/// <summary>
/// Validates a <see cref="RegisterIncomeCommand"/> using synchronous
/// FluentValidation rules. Async validators are intentionally avoided
/// to prevent <c>AsyncValidatorInvokedSynchronouslyException</c>.
/// </summary>
/// <remarks>
/// <see cref="RegisterIncomeCommand.CategoryId"/> is nullable: the
/// <c>NotEqual(Guid.Empty)</c> rule only applies when a category is
/// supplied (<c>.When(c => c.CategoryId is not null)</c>). A null
/// category is valid (an income may have no category).
/// </remarks>
public sealed class RegisterIncomeValidator : AbstractValidator<RegisterIncomeCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterIncomeValidator"/> class.
    /// </summary>
    public RegisterIncomeValidator()
    {
        RuleFor(c => c.CategoryId)
            .NotEqual(Guid.Empty)
            .When(c => c.CategoryId is not null)
            .WithMessage("La categoría no es válida.");

        RuleFor(c => c.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor que 0.");

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(200).WithMessage("La descripción no puede superar 200 caracteres.")
            .Must(d => d.Trim().Length > 0).WithMessage("La descripción no puede contener solo espacios en blanco.");

        RuleFor(c => c.Note)
            .MaximumLength(500)
            .When(c => c.Note is not null)
            .WithMessage("La nota no puede superar 500 caracteres.");

        RuleFor(c => c.Currency)
            .IsInEnum().WithMessage("La moneda no es válida.");

        RuleFor(c => c.PaymentMethodId)
            .Must(id => id == null || id != Guid.Empty)
            .WithMessage("El método de pago no es válido.");
    }
}
