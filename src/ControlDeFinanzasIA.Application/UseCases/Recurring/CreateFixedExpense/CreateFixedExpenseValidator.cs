using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;

public sealed class CreateFixedExpenseValidator : AbstractValidator<CreateFixedExpenseCommand>
{
    public CreateFixedExpenseValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Frequency).Must(x => x is "Monthly" or "Bimonthly" or "Yearly");
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue);
    }
}
