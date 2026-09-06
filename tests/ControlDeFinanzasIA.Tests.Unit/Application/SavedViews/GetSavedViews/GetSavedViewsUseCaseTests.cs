using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.GetSavedViews;

/// <summary>
/// Unit tests for <see cref="GetSavedViewsUseCase"/>.
/// Uses hand-rolled fakes and pure xUnit assertions.
/// </summary>
public sealed class GetSavedViewsUseCaseTests
{
    private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeSavedViewRepository _repository;
    private readonly GetSavedViewsUseCase _useCase;

    public GetSavedViewsUseCaseTests()
    {
        _repository = new FakeSavedViewRepository();
        _useCase = new GetSavedViewsUseCase(_repository);
    }

    private async Task<SavedView> CreateSavedViewAsync(string name, TransactionType entityType)
    {
        string filterJson = entityType == TransactionType.Expense
            ? """{"schemaVersion":1,"type":"ExpenseFilter","data":{"page":1,"pageSize":50}}"""
            : """{"schemaVersion":1,"type":"IncomeFilter","data":{"page":1,"pageSize":50}}""";
        var view = SavedView.Create(name, entityType, filterJson, _fixedTimestamp);
        await _repository.AddAsync(view);
        return view;
    }

    [Fact]
    public async Task ExecuteAsync_WithEntityTypeFilter_ReturnsMatching()
    {
        await CreateSavedViewAsync("Gasto A", TransactionType.Expense);
        await CreateSavedViewAsync("Gasto B", TransactionType.Expense);
        await CreateSavedViewAsync("Ingreso A", TransactionType.Income);

        Result result = await _useCase.ExecuteAsync(new GetSavedViewsQuery(TransactionType.Expense));

        Assert.True(result.IsSuccess);
        IReadOnlyList<SavedView> views = Assert.IsAssignableFrom<IReadOnlyList<SavedView>>(result.Value);
        Assert.Equal(2, views.Count);
        Assert.All(views, v => Assert.Equal(TransactionType.Expense, v.EntityType));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutEntityType_ReturnsAll()
    {
        await CreateSavedViewAsync("Gasto A", TransactionType.Expense);
        await CreateSavedViewAsync("Gasto B", TransactionType.Expense);
        await CreateSavedViewAsync("Ingreso A", TransactionType.Income);

        Result result = await _useCase.ExecuteAsync(new GetSavedViewsQuery());

        Assert.True(result.IsSuccess);
        IReadOnlyList<SavedView> views = Assert.IsAssignableFrom<IReadOnlyList<SavedView>>(result.Value);
        Assert.Equal(3, views.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoSavedViews_ReturnsEmpty()
    {
        Result result = await _useCase.ExecuteAsync(new GetSavedViewsQuery());

        Assert.True(result.IsSuccess);
        IReadOnlyList<SavedView> views = Assert.IsAssignableFrom<IReadOnlyList<SavedView>>(result.Value);
        Assert.Empty(views);
    }
}
