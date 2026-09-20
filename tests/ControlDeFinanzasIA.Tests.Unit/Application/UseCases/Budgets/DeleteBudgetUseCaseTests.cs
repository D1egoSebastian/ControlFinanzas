using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Budgets.DeleteBudget;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Budgets;

/// <summary>
/// Tests for <see cref="DeleteBudgetUseCase"/> (R-1: hard delete, no
/// soft-delete/archival concept).
/// </summary>
public sealed class DeleteBudgetUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ExistingBudget_RemovesItFromTheRepository()
    {
        var repository = new FakeBudgetRepository();
        var budget = Budget.Create(2026, 9, Guid.NewGuid(), Money.Create(10000m, Currency.DOP));
        repository.Seed(budget);
        var useCase = new DeleteBudgetUseCase(repository);

        Result result = await useCase.ExecuteAsync(budget.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Budgets);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownId_PropagatesNotFoundFailure()
    {
        var repository = new FakeBudgetRepository
        {
            RemoveResult = Result.Failure("NotFound", "Budget not found."),
        };
        var useCase = new DeleteBudgetUseCase(repository);

        Result result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error!.Code);
    }
}
