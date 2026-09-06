using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.SaveCurrentFilter;

/// <summary>
/// Unit tests for <see cref="SaveCurrentFilterUseCase"/>.
/// Uses hand-rolled fakes and pure xUnit assertions.
/// </summary>
public sealed class SaveCurrentFilterUseCaseTests
{
    private static readonly Guid _categoryId = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _tag1 = new("22222222-2222-2222-2222-000000000001");

    private readonly FakeClock _clock;
    private readonly FakeSavedViewRepository _repository;
    private readonly SaveCurrentFilterValidator _validator;
    private readonly SaveCurrentFilterUseCase _useCase;

    public SaveCurrentFilterUseCaseTests()
    {
        _clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        };
        _repository = new FakeSavedViewRepository();
        _validator = new SaveCurrentFilterValidator();
        _useCase = new SaveCurrentFilterUseCase(
            _repository,
            _clock,
            _validator,
            NullLogger<SaveCurrentFilterUseCase>.Instance);
    }

    private SaveCurrentFilterCommand CreateValidCommand(TransactionType entityType)
    {
        object filter = entityType == TransactionType.Expense
            ? new ExpenseFilter
            {
                Page = 2,
                PageSize = 25,
                CategoryId = _categoryId,
                DateFrom = new DateOnly(2026, 1, 1),
                DateTo = new DateOnly(2026, 1, 31),
                TagIds = new[] { _tag1 },
            }
            : new IncomeFilter
            {
                Page = 1,
                PageSize = 50,
                CategoryId = _categoryId,
                DateFrom = new DateOnly(2026, 2, 1),
                DateTo = new DateOnly(2026, 2, 28),
                TagIds = new[] { _tag1 },
            };

        return new SaveCurrentFilterCommand("Transporte", entityType, filter);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidExpenseFilter_Persists()
    {
        SaveCurrentFilterCommand command = CreateValidCommand(TransactionType.Expense);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        SavedView persisted = Assert.IsType<SavedView>(result.Value);
        Assert.Equal("Transporte", persisted.Name);
        Assert.Equal(TransactionType.Expense, persisted.EntityType);
        Assert.Equal(_clock.UtcNow, persisted.CreatedAt);
        Assert.Equal(_clock.UtcNow, persisted.UpdatedAt);

        SavedView captured = Assert.Single(_repository.AddedSavedViews);
        Assert.Equal(persisted.Id, captured.Id);
        Assert.Contains("\"type\":\"ExpenseFilter\"", captured.FilterJson);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidIncomeFilter_Persists()
    {
        SaveCurrentFilterCommand command = CreateValidCommand(TransactionType.Income);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        SavedView persisted = Assert.IsType<SavedView>(result.Value);
        Assert.Equal("Transporte", persisted.Name);
        Assert.Equal(TransactionType.Income, persisted.EntityType);

        SavedView captured = Assert.Single(_repository.AddedSavedViews);
        Assert.Contains("\"type\":\"IncomeFilter\"", captured.FilterJson);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task ExecuteAsync_WithEmptyName_ReturnsValidationFailure(string name)
    {
        SaveCurrentFilterCommand command = new(name, TransactionType.Expense, new ExpenseFilter());

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Name", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedSavedViews);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateName_ReturnsDuplicateName()
    {
        _repository.AddResultOverride = Result.Failure(
            "DuplicateName",
            "Ya existe una vista guardada con el nombre 'Transporte'.");
        SaveCurrentFilterCommand command = CreateValidCommand(TransactionType.Expense);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DuplicateName", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AssignsSchemaVersionFromSerializer()
    {
        SaveCurrentFilterCommand command = CreateValidCommand(TransactionType.Expense);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.True(result.IsSuccess);
        SavedView persisted = Assert.IsType<SavedView>(result.Value);
        Assert.Equal(FilterJsonSerializer.CurrentSchemaVersion, persisted.SchemaVersion);
        Assert.Contains(
            $"\"schemaVersion\":{FilterJsonSerializer.CurrentSchemaVersion}",
            persisted.FilterJson);
    }

    [Fact]
    public async Task ExecuteAsync_WithNameOver100Chars_ReturnsValidationFailure()
    {
        SaveCurrentFilterCommand command = new(
            new string('x', 101),
            TransactionType.Expense,
            new ExpenseFilter());

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Name", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedSavedViews);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullFilter_ReturnsValidationFailure()
    {
        SaveCurrentFilterCommand command = new("Transporte", TransactionType.Expense, null!);

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Filter", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedSavedViews);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedFilterType_ReturnsValidationFailure()
    {
        SaveCurrentFilterCommand command = new("Transporte", TransactionType.Expense, "not a filter");

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(_repository.AddedSavedViews);
    }

    [Fact]
    public async Task ExecuteAsync_WithEntityTypeFilterMismatch_ReturnsValidationFailure()
    {
        SaveCurrentFilterCommand command = new(
            "Transporte",
            TransactionType.Income,
            new ExpenseFilter());

        Result result = await _useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(_repository.AddedSavedViews);
    }
}
