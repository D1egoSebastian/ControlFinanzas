using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;

public sealed class UpdateSubscriptionValidator : AbstractValidator<UpdateSubscriptionCommand>
{
    public UpdateSubscriptionValidator()
    {
        RuleFor(c => c.Id).NotEqual(Guid.Empty);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Amount).GreaterThan(0);
        RuleFor(c => c.Frequency)
            .Must(f => f is "Monthly" or "Bimonthly" or "Yearly");
        RuleFor(c => c.StartDate).GreaterThanOrEqualTo(new DateOnly(1900, 1, 1));
        RuleFor(c => c.EndDate)
            .GreaterThanOrEqualTo(c => c.StartDate)
            .When(c => c.EndDate.HasValue);
        RuleFor(c => c.CategoryId).NotEqual(Guid.Empty);
        RuleFor(c => c.PaymentMethodId).NotEqual(Guid.Empty);
    }
}
