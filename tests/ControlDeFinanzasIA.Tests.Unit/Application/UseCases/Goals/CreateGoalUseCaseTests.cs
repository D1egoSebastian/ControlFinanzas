using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Goals.CreateGoal;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Goals;

/// <summary>
/// Tests for <see cref="CreateGoalUseCase"/> (R-1, R-2, S-1, S-3, S-4).
/// </summary>
public sealed class CreateGoalUseCaseTests
{
    private static CreateGoalUseCase CreateUseCase(FakeGoalRepository repository) =>
        new(repository, new CreateGoalValidator(), NullLogger<CreateGoalUseCase>.Instance);

    private static CreateGoalCommand ValidCommand() =>
        new("Fondo de emergencia", 200000m, Currency.DOP, new DateOnly(2027, 6, 30), null);

    [Fact]
    public async Task ExecuteAsync_ValidCommand_AddsGoalAndReturnsSuccess()
    {
        var repository = new FakeGoalRepository();
        CreateGoalUseCase useCase = CreateUseCase(repository);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.True(result.IsSuccess);
        Assert.True(repository.AddAsyncCalled);
        Goal goal = Assert.Single(repository.Goals);
        Assert.Equal("Fondo de emergencia", goal.Name);
        Assert.Equal(200000m, goal.TargetAmount.Amount);
        Assert.Equal(Currency.DOP, goal.Currency);
    }

    [Fact]
    public async Task ExecuteAsync_PastTargetDate_SucceedsWithoutRejection()
    {
        var repository = new FakeGoalRepository();
        CreateGoalUseCase useCase = CreateUseCase(repository);
        CreateGoalCommand command = ValidCommand() with { TargetDate = new DateOnly(2020, 1, 1) };

        Result result = await useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        Goal goal = Assert.Single(repository.Goals);
        Assert.Equal(new DateOnly(2020, 1, 1), goal.TargetDate);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyName_ReturnsValidationFailureWithoutWriting()
    {
        var repository = new FakeGoalRepository();
        CreateGoalUseCase useCase = CreateUseCase(repository);
        CreateGoalCommand command = ValidCommand() with { Name = "   " };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(repository.Goals);
        Assert.False(repository.AddAsyncCalled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task ExecuteAsync_NonPositiveAmount_ReturnsValidationFailureWithoutWriting(decimal amount)
    {
        var repository = new FakeGoalRepository();
        CreateGoalUseCase useCase = CreateUseCase(repository);
        CreateGoalCommand command = ValidCommand() with { TargetAmount = amount };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(repository.Goals);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryFailure_PropagatesErrorAndDoesNotExposeSuccess()
    {
        var repository = new FakeGoalRepository
        {
            AddResult = Result.Failure("RepositoryError", "boom"),
        };
        CreateGoalUseCase useCase = CreateUseCase(repository);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal("RepositoryError", result.Error!.Code);
        Assert.Empty(repository.Goals);
    }
}
