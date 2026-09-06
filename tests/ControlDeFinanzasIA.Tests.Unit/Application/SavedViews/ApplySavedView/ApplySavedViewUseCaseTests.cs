using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.ApplySavedView;

/// <summary>
/// Unit tests for <see cref="ApplySavedViewUseCase"/>.
/// Uses hand-rolled fakes and pure xUnit assertions.
/// </summary>
public sealed class ApplySavedViewUseCaseTests
{
    private static readonly Guid _categoryId = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _tag1 = new("22222222-2222-2222-2222-000000000001");
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeSavedViewRepository _repository;
    private readonly ApplySavedViewUseCase _useCase;

    public ApplySavedViewUseCaseTests()
    {
        _repository = new FakeSavedViewRepository();
        _useCase = new ApplySavedViewUseCase(
            _repository,
            NullLogger<ApplySavedViewUseCase>.Instance);
    }

    private async Task<SavedView> CreateSavedViewAsync(string name, TransactionType entityType)
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

        string filterJson = FilterJsonSerializer.Serialize(filter, filter.GetType());
        var view = SavedView.Create(name, entityType, filterJson, _fixedTimestamp);
        await _repository.AddAsync(view);
        return view;
    }

    [Fact]
    public async Task ExecuteAsync_WithExpenseSavedView_ReturnsExpenseFilter()
    {
        SavedView view = await CreateSavedViewAsync("Transporte", TransactionType.Expense);

        Result result = await _useCase.ExecuteAsync(new ApplySavedViewQuery(view.Id));

        Assert.True(result.IsSuccess);
        ApplySavedViewResult actual = Assert.IsType<ApplySavedViewResult>(result.Value);
        Assert.Equal(TransactionType.Expense, actual.EntityType);
        ExpenseFilter filter = Assert.IsType<ExpenseFilter>(actual.Filter);
        Assert.Equal(2, filter.Page);
        Assert.Equal(25, filter.PageSize);
        Assert.Equal(_categoryId, filter.CategoryId);
        Assert.Equal(new DateOnly(2026, 1, 1), filter.DateFrom);
        Assert.Equal(new DateOnly(2026, 1, 31), filter.DateTo);
        Assert.NotNull(filter.TagIds);
        Assert.Single(filter.TagIds!);
    }

    [Fact]
    public async Task ExecuteAsync_WithIncomeSavedView_ReturnsIncomeFilter()
    {
        SavedView view = await CreateSavedViewAsync("Sueldo", TransactionType.Income);

        Result result = await _useCase.ExecuteAsync(new ApplySavedViewQuery(view.Id));

        Assert.True(result.IsSuccess);
        ApplySavedViewResult actual = Assert.IsType<ApplySavedViewResult>(result.Value);
        Assert.Equal(TransactionType.Income, actual.EntityType);
        IncomeFilter filter = Assert.IsType<IncomeFilter>(actual.Filter);
        Assert.Equal(1, filter.Page);
        Assert.Equal(50, filter.PageSize);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentId_ReturnsNotFound()
    {
        Result result = await _useCase.ExecuteAsync(new ApplySavedViewQuery(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyGuid_ReturnsNotFound()
    {
        Result result = await _useCase.ExecuteAsync(new ApplySavedViewQuery(Guid.Empty));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithFutureSchemaVersion_ReturnsIncompatibleVersion()
    {
        const string futureJson =
            """{"schemaVersion":2,"type":"ExpenseFilter","data":{"page":1,"pageSize":50}}""";
        var view = SavedView.Create("Futuro", TransactionType.Expense, futureJson, _fixedTimestamp);
        await _repository.AddAsync(view);

        Result result = await _useCase.ExecuteAsync(new ApplySavedViewQuery(view.Id));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("IncompatibleVersion", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithCorruptedFilterJson_ReturnsCorruptedFilter()
    {
        var view = SavedView.Create("Corrupto", TransactionType.Expense, "not json", _fixedTimestamp);
        await _repository.AddAsync(view);

        Result result = await _useCase.ExecuteAsync(new ApplySavedViewQuery(view.Id));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("CorruptedFilter", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithLegacyJsonWithoutSchemaVersion_ReturnsFilter()
    {
        const string legacyJson =
            """{"type":"ExpenseFilter","data":{"page":3,"pageSize":10,"categoryId":null,"dateFrom":null,"dateTo":null,"tagIds":null}}""";
        var view = SavedView.Create("Legacy", TransactionType.Expense, legacyJson, _fixedTimestamp);
        await _repository.AddAsync(view);

        Result result = await _useCase.ExecuteAsync(new ApplySavedViewQuery(view.Id));

        Assert.True(result.IsSuccess);
        ApplySavedViewResult actual = Assert.IsType<ApplySavedViewResult>(result.Value);
        ExpenseFilter filter = Assert.IsType<ExpenseFilter>(actual.Filter);
        Assert.Equal(3, filter.Page);
        Assert.Equal(10, filter.PageSize);
    }
}
