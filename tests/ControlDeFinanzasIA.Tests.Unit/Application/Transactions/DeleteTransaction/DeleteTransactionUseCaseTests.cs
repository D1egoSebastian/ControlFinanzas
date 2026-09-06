using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Transactions.DeleteTransaction;

/// <summary>
/// Unit tests for <see cref="DeleteTransactionUseCase"/>.
/// Uses hand-rolled fakes for <see cref="IExpenseRepository"/> and
/// <see cref="IIncomeRepository"/> and pure xUnit assertions.
/// </summary>
public sealed class DeleteTransactionUseCaseTests
{
    private readonly FakeExpenseRepository _expenseRepository;
    private readonly FakeIncomeRepository _incomeRepository;
    private readonly DeleteTransactionUseCase _useCase;

    public DeleteTransactionUseCaseTests()
    {
        _expenseRepository = new FakeExpenseRepository();
        _incomeRepository = new FakeIncomeRepository();
        _useCase = new DeleteTransactionUseCase(
            _expenseRepository,
            _incomeRepository,
            NullLogger<DeleteTransactionUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpenseType_CallsExpenseRepository()
    {
        var id = Guid.NewGuid();
        _expenseRepository.RemoveResultOverride = Result.Success(null!);
        _incomeRepository.RemoveResultOverride = Result.Failure("ShouldNotInvoke", "Income repository must not be called.");
        var command = new DeleteTransactionCommand(id, TransactionType.Expense);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_WithIncomeType_CallsIncomeRepository()
    {
        var id = Guid.NewGuid();
        _incomeRepository.RemoveResultOverride = Result.Success(null!);
        _expenseRepository.RemoveResultOverride = Result.Failure("ShouldNotInvoke", "Expense repository must not be called.");
        var command = new DeleteTransactionCommand(id, TransactionType.Income);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyId_ReturnsValidationFailure()
    {
        var command = new DeleteTransactionCommand(Guid.Empty, TransactionType.Expense);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithNotFoundExpense_PropagatesResult()
    {
        var id = Guid.NewGuid();
        _expenseRepository.RemoveResultOverride = Result.Failure("NotFound", "Expense not found.");
        var command = new DeleteTransactionCommand(id, TransactionType.Expense);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithNotFoundIncome_PropagatesResult()
    {
        var id = Guid.NewGuid();
        _incomeRepository.RemoveResultOverride = Result.Failure("NotFound", "Income not found.");
        var command = new DeleteTransactionCommand(id, TransactionType.Income);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }
}
