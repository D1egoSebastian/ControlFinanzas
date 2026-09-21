using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Goals.DeleteGoal;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Goals;

/// <summary>
/// Tests for <see cref="DeleteGoalUseCase"/> (R-1: hard delete, D-8).
/// </summary>
public sealed class DeleteGoalUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ExistingGoal_RemovesItFromTheRepository()
    {
        var repository = new FakeGoalRepository();
        var goal = Goal.Create("Fondo de emergencia", Money.Create(200000m, Currency.DOP), new DateOnly(2027, 6, 30));
        repository.Seed(goal);
        var useCase = new DeleteGoalUseCase(repository);

        Result result = await useCase.ExecuteAsync(goal.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Goals);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownId_PropagatesNotFoundFailure()
    {
        var repository = new FakeGoalRepository
        {
            RemoveResult = Result.Failure("NotFound", "Goal not found."),
        };
        var useCase = new DeleteGoalUseCase(repository);

        Result result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error!.Code);
    }
}
