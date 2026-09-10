using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Recurring.CreateSubscription;

/// <summary>
/// Tests para <see cref="CreateSubscriptionUseCase"/>.
/// </summary>
public sealed class CreateSubscriptionUseCaseTests
{
    private static CreateSubscriptionCommand CreateValidCommand() => new(
        Name: "Netflix",
        Amount: 15.99m,
        Currency: Currency.DOP,
        Frequency: "Monthly",
        StartDate: new DateOnly(2026, 1, 15),
        EndDate: null,
        CategoryId: Guid.NewGuid(),
        PaymentMethodId: Guid.NewGuid(),
        TagIds: null);

    [Fact]
    public async Task ExecuteAsync_ValidCommand_PersistsAndReturnsSubscription()
    {
        FakeRecurringRuleRepository repo = new();
        IValidator<CreateSubscriptionCommand> validator = new CreateSubscriptionValidator();
        var useCase = new CreateSubscriptionUseCase(
            repo, validator, NullLogger<CreateSubscriptionUseCase>.Instance);

        Result result = await useCase.ExecuteAsync(CreateValidCommand());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Subscription persisted = Assert.IsType<Subscription>(result.Value);
        Assert.Equal("Netflix", persisted.Name);
        Assert.Equal(15.99m, persisted.Amount.Amount);
        Assert.Equal(Currency.DOP, persisted.Currency);
        Assert.Equal("Monthly", persisted.Frequency);
        Assert.Equal(new DateOnly(2026, 1, 15), persisted.StartDate);
        Assert.True(persisted.Active);
        Assert.Equal(new DateOnly(2026, 1, 15), persisted.NextGenerationDate);
        Assert.Single(repo.Added);
        Assert.Equal(persisted.Id, repo.Added[0].Id);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyName_ReturnsValidationFailure()
    {
        FakeRecurringRuleRepository repo = new();
        IValidator<CreateSubscriptionCommand> validator = new CreateSubscriptionValidator();
        var useCase = new CreateSubscriptionUseCase(
            repo, validator, NullLogger<CreateSubscriptionUseCase>.Instance);

        CreateSubscriptionCommand cmd = CreateValidCommand() with { Name = string.Empty };
        Result result = await useCase.ExecuteAsync(cmd);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Name", result.Error.Details!.Keys);
        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidFrequency_ReturnsValidationFailure()
    {
        FakeRecurringRuleRepository repo = new();
        IValidator<CreateSubscriptionCommand> validator = new CreateSubscriptionValidator();
        var useCase = new CreateSubscriptionUseCase(
            repo, validator, NullLogger<CreateSubscriptionUseCase>.Instance);

        CreateSubscriptionCommand cmd = CreateValidCommand() with { Frequency = "Weekly" };
        Result result = await useCase.ExecuteAsync(cmd);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeAmount_ReturnsValidationFailure()
    {
        FakeRecurringRuleRepository repo = new();
        IValidator<CreateSubscriptionCommand> validator = new CreateSubscriptionValidator();
        var useCase = new CreateSubscriptionUseCase(
            repo, validator, NullLogger<CreateSubscriptionUseCase>.Instance);

        CreateSubscriptionCommand cmd = CreateValidCommand() with { Amount = -1m };
        Result result = await useCase.ExecuteAsync(cmd);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCategoryId_ReturnsValidationFailure()
    {
        FakeRecurringRuleRepository repo = new();
        IValidator<CreateSubscriptionCommand> validator = new CreateSubscriptionValidator();
        var useCase = new CreateSubscriptionUseCase(
            repo, validator, NullLogger<CreateSubscriptionUseCase>.Instance);

        CreateSubscriptionCommand cmd = CreateValidCommand() with { CategoryId = Guid.Empty };
        Result result = await useCase.ExecuteAsync(cmd);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryFails_PropagatesFailure()
    {
        var repo = new FakeRecurringRuleRepository
        {
            AddResult = Result.Failure("DBError", "Disk full"),
        };
        IValidator<CreateSubscriptionCommand> validator = new CreateSubscriptionValidator();
        var useCase = new CreateSubscriptionUseCase(
            repo, validator, NullLogger<CreateSubscriptionUseCase>.Instance);

        Result result = await useCase.ExecuteAsync(CreateValidCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal("DBError", result.Error!.Code);
    }
}
