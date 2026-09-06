using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;

/// <summary>
/// Validates a <see cref="CreateTagCommand"/> using synchronous
/// FluentValidation rules. Async validators are intentionally avoided
/// to prevent <c>AsyncValidatorInvokedSynchronouslyException</c>.
/// </summary>
public sealed class CreateTagValidator : AbstractValidator<CreateTagCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTagValidator"/> class.
    /// </summary>
    public CreateTagValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("El nombre de la etiqueta es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre no puede superar 50 caracteres.")
            .Must(n => n.Trim().Length > 0).WithMessage("El nombre no puede contener solo espacios en blanco.");

        RuleFor(c => c.Color)
            .Must(c => c is not null && c.Trim().Length <= 9)
            .When(c => c.Color is not null)
            .WithMessage("El color no puede superar 9 caracteres.");
    }
}
