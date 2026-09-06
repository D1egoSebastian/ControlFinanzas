using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;

/// <summary>
/// Validates a <see cref="SaveCurrentFilterCommand"/> before the use case
/// attempts to persist the saved view.
/// </summary>
public sealed class SaveCurrentFilterValidator : AbstractValidator<SaveCurrentFilterCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SaveCurrentFilterValidator"/> class.
    /// </summary>
    public SaveCurrentFilterValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(100)
            .Must(n => n.Trim().Length > 0)
            .WithMessage("El nombre no puede estar vacío.");

        RuleFor(c => c.Filter)
            .NotNull()
            .WithMessage("El filtro no puede ser nulo.");
    }
}
