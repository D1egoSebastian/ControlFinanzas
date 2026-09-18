using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Recurring.UpdateSubscription;

public sealed class UpdateSubscriptionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ValidCommand_UpdatesExistingSubscription()
    {
        FakeRecurringRuleRepository repository = new();
        Subscription existing = RecurringRule.Create(
            "Netflix",
            Money.Create(15m, Currency.DOP),
            Currency.DOP,
            "Monthly",
            new DateOnly(2026, 1, 15),
            null,
            Guid.NewGuid(),
            Guid.NewGuid());
        repository.Seed(existing);

        var useCase = new UpdateSubscriptionUseCase(
            repository,
            new UpdateSubscriptionValidator(),
            NullLogger<UpdateSubscriptionUseCase>.Instance);

        Result result = await useCase.ExecuteAsync(new UpdateSubscriptionCommand(
            existing.Id,
            "Netflix Premium",
            20m,
            Currency.USD,
            "Bimonthly",
            new DateOnly(2026, 2, 1),
            null,
            existing.CategoryId,
            existing.PaymentMethodId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Netflix Premium", existing.Name);
        Assert.Equal(20m, existing.Amount.Amount);
        Assert.Equal(Currency.USD, existing.Currency);
        Assert.Equal("Bimonthly", existing.Frequency);
    }

    [Fact]
    public async Task ExecuteAsync_MissingSubscription_ReturnsNotFound()
    {
        var useCase = new UpdateSubscriptionUseCase(
            new FakeRecurringRuleRepository(),
            new UpdateSubscriptionValidator(),
            NullLogger<UpdateSubscriptionUseCase>.Instance);

        Result result = await useCase.ExecuteAsync(new UpdateSubscriptionCommand(
            Guid.NewGuid(), "Netflix", 20m, Currency.DOP, "Monthly",
            new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error!.Code);
    }
}
