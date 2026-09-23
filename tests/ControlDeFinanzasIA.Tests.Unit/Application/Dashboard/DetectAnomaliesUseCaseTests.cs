using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Dashboard;

/// <summary>
/// Unit tests for <see cref="DetectAnomaliesUseCase"/> (F-014, slice S4,
/// D-7). The detector's own math (band edges, MAD=0 fallback, eligibility)
/// is covered by <c>AnomalyDetectorTests</c>; these tests cover the use
/// case's wiring: the 7-month window query, DOP normalization, the
/// reconciliation table (S-1, S-2, S-3, re-dated, deleted, unconverted-kept)
/// and mixed-currency history (S-10).
/// </summary>
public sealed class DetectAnomaliesUseCaseTests
{
    private static readonly DateTime _fixedCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _fixedDetectedAt = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly _anchor = new(2026, 3, 1);

    /// <summary>
    /// Seeds the worked-example history (design D-1): 8 expenses across 3
    /// distinct months → median 100, MAD 5. A 145 candidate scores z=6.07
    /// (Warning).
    /// </summary>
    private static void SeedWorkedExampleHistory(FakeExpenseRepository expenseRepo, Guid categoryId)
    {
        (DateOnly Date, decimal Amount)[] history =
        [
            (new DateOnly(2025, 12, 3), 100m),
            (new DateOnly(2025, 12, 20), 100m),
            (new DateOnly(2026, 1, 3), 110m),
            (new DateOnly(2026, 1, 15), 90m),
            (new DateOnly(2026, 1, 28), 100m),
            (new DateOnly(2026, 2, 3), 120m),
            (new DateOnly(2026, 2, 15), 80m),
            (new DateOnly(2026, 2, 26), 100m),
        ];

        foreach ((DateOnly date, decimal amount) in history)
        {
            expenseRepo.AddAsync(CreateExpense(categoryId, date, amount, Currency.DOP)).GetAwaiter().GetResult();
        }
    }

    private static Expense CreateExpense(Guid categoryId, DateOnly date, decimal amount, Currency currency)
        => Expense.Create(categoryId, date, Money.Create(amount, currency), "Test expense", null, _fixedCreatedAt);

    private static DetectAnomaliesUseCase CreateUseCase(
        FakeExpenseRepository expenseRepo,
        FakeCategoryRepository? categoryRepo = null,
        FakeAnomalyAlertRepository? alertRepo = null,
        FakeCurrencyConverter? converter = null,
        FakeAnomalyMessageFormatter? formatter = null,
        FakeClock? clock = null)
        => new(
            expenseRepo,
            categoryRepo ?? new FakeCategoryRepository(),
            alertRepo ?? new FakeAnomalyAlertRepository(),
            converter ?? new FakeCurrencyConverter(),
            formatter ?? new FakeAnomalyMessageFormatter(),
            clock ?? new FakeClock { Today = new DateOnly(2026, 3, 10), UtcNow = _fixedDetectedAt },
            new RecordingLogger<DetectAnomaliesUseCase>());

    [Fact]
    public async Task ExecuteAsync_ThinHistory_ProducesNoAlert()
    {
        // Only 2 prior expenses -- below MinHistoryCount (8) and MinHistoryMonths (3).
        var categoryId = Guid.NewGuid();
        FakeExpenseRepository expenseRepo = new();
        await expenseRepo.AddAsync(CreateExpense(categoryId, new DateOnly(2026, 2, 3), 100m, Currency.DOP));
        await expenseRepo.AddAsync(CreateExpense(categoryId, new DateOnly(2026, 2, 15), 100m, Currency.DOP));
        Expense candidate = CreateExpense(categoryId, new DateOnly(2026, 3, 5), 100000m, Currency.DOP);
        await expenseRepo.AddAsync(candidate);

        FakeAnomalyAlertRepository alertRepo = new();
        DetectAnomaliesUseCase useCase = CreateUseCase(expenseRepo, alertRepo: alertRepo);

        Result result = await useCase.ExecuteAsync(_anchor);

        Assert.True(result.IsSuccess);
        AnomalyDetectionResult detection = Assert.IsType<AnomalyDetectionResult>(result.Value);
        Assert.Empty(detection.Items);
        Assert.Empty(alertRepo.Alerts);
    }

    [Fact]
    public async Task ExecuteAsync_ClearOutlier_TwoConsecutiveLoadsPersistExactlyOneAlert()
    {
        var categoryId = Guid.NewGuid();
        FakeExpenseRepository expenseRepo = new();
        SeedWorkedExampleHistory(expenseRepo, categoryId);
        Expense candidate = CreateExpense(categoryId, new DateOnly(2026, 3, 5), 145m, Currency.DOP);
        await expenseRepo.AddAsync(candidate);

        FakeCategoryRepository categoryRepo = new();
        categoryRepo.SetCategories([new Category { Id = categoryId, Name = "Supermercado" }]);
        FakeAnomalyAlertRepository alertRepo = new();
        DetectAnomaliesUseCase useCase = CreateUseCase(expenseRepo, categoryRepo, alertRepo);

        Result first = await useCase.ExecuteAsync(_anchor);
        Result second = await useCase.ExecuteAsync(_anchor);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single(alertRepo.Alerts, a => a.TransactionId == candidate.Id);

        AnomalyDetectionResult detection = Assert.IsType<AnomalyDetectionResult>(second.Value);
        AnomalyItemDto item = Assert.Single(detection.Items);
        Assert.Equal(candidate.Id, item.ExpenseId);
        Assert.Equal(AnomalySeverity.Warning, item.Severity);
        Assert.Equal("Supermercado", item.CategoryName);
    }

    [Fact]
    public async Task ExecuteAsync_AcknowledgedAlert_IsNeverModifiedOrRecreated()
    {
        var categoryId = Guid.NewGuid();
        FakeExpenseRepository expenseRepo = new();
        SeedWorkedExampleHistory(expenseRepo, categoryId);
        Expense candidate = CreateExpense(categoryId, new DateOnly(2026, 3, 5), 145m, Currency.DOP);
        await expenseRepo.AddAsync(candidate);

        var acknowledged = AnomalyAlert.Create(
            candidate.Id, categoryId, new DateOnly(2026, 3, 5), AnomalySeverity.Warning, "Pre-existing", _fixedDetectedAt);
        acknowledged.Acknowledge(new DateTime(2026, 3, 6, 8, 0, 0, DateTimeKind.Utc));

        FakeAnomalyAlertRepository alertRepo = new();
        alertRepo.Seed(acknowledged);

        DetectAnomaliesUseCase useCase = CreateUseCase(expenseRepo, alertRepo: alertRepo);

        Result result = await useCase.ExecuteAsync(_anchor);

        Assert.True(result.IsSuccess);
        AnomalyAlert persisted = Assert.Single(alertRepo.Alerts);
        Assert.True(persisted.IsAcknowledged);
        Assert.Equal("Pre-existing", persisted.Message);
        Assert.Equal(AnomalySeverity.Warning, persisted.Severity);
        Assert.Equal(new DateTime(2026, 3, 6, 8, 0, 0, DateTimeKind.Utc), persisted.AcknowledgedAt);

        AnomalyDetectionResult detection = Assert.IsType<AnomalyDetectionResult>(result.Value);
        Assert.Empty(detection.Items); // acknowledged alerts are never returned as active items
    }

    [Fact]
    public async Task ExecuteAsync_ReDatedExpenseMovedIntoMonth_RefreshesExistingAlertKeepingStableId()
    {
        var categoryId = Guid.NewGuid();
        FakeExpenseRepository expenseRepo = new();
        SeedWorkedExampleHistory(expenseRepo, categoryId);
        Expense candidate = CreateExpense(categoryId, new DateOnly(2026, 3, 5), 145m, Currency.DOP);
        await expenseRepo.AddAsync(candidate);

        // Stale alert from a prior detection, dated in February (before the
        // expense was re-dated into March).
        var stale = AnomalyAlert.Create(
            candidate.Id, categoryId, new DateOnly(2026, 2, 20), AnomalySeverity.Info, "Old message", _fixedDetectedAt);
        FakeAnomalyAlertRepository alertRepo = new();
        alertRepo.Seed(stale);

        DetectAnomaliesUseCase useCase = CreateUseCase(expenseRepo, alertRepo: alertRepo);

        Result result = await useCase.ExecuteAsync(_anchor);

        Assert.True(result.IsSuccess);
        AnomalyAlert persisted = Assert.Single(alertRepo.Alerts);
        Assert.Equal(stale.Id, persisted.Id); // stable id preserved
        Assert.Equal(new DateOnly(2026, 3, 5), persisted.Date);
        Assert.Equal(AnomalySeverity.Warning, persisted.Severity);
        Assert.NotEqual("Old message", persisted.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DeletedExpense_RemovesItsUnacknowledgedAlert()
    {
        var categoryId = Guid.NewGuid();
        FakeExpenseRepository expenseRepo = new();
        SeedWorkedExampleHistory(expenseRepo, categoryId);
        // No candidate expense added: the expense that originated this alert was deleted.
        var deletedExpenseId = Guid.NewGuid();
        var orphan = AnomalyAlert.Create(
            deletedExpenseId, categoryId, new DateOnly(2026, 3, 5), AnomalySeverity.Warning, "Orphan", _fixedDetectedAt);
        FakeAnomalyAlertRepository alertRepo = new();
        alertRepo.Seed(orphan);

        DetectAnomaliesUseCase useCase = CreateUseCase(expenseRepo, alertRepo: alertRepo);

        Result result = await useCase.ExecuteAsync(_anchor);

        Assert.True(result.IsSuccess);
        Assert.Empty(alertRepo.Alerts);
    }

    [Fact]
    public async Task ExecuteAsync_UnconvertedExpense_KeepsExistingAlertInsteadOfRemovingIt()
    {
        var categoryId = Guid.NewGuid();
        FakeExpenseRepository expenseRepo = new();
        SeedWorkedExampleHistory(expenseRepo, categoryId);
        Expense candidate = CreateExpense(categoryId, new DateOnly(2026, 3, 5), 145m, Currency.USD);
        await expenseRepo.AddAsync(candidate);

        var existing = AnomalyAlert.Create(
            candidate.Id, categoryId, new DateOnly(2026, 3, 5), AnomalySeverity.Warning, "Existing", _fixedDetectedAt);
        FakeAnomalyAlertRepository alertRepo = new();
        alertRepo.Seed(existing);

        // The rate provider is down for USD this load -- "cannot evaluate" is
        // not "no longer qualifies" (Spec Reconciliation #2).
        FakeCurrencyConverter converter = new(onConvert: (money, _) =>
            money.Currency == Currency.USD
                ? Result.Failure("ExchangeRateUnavailable", "Provider unavailable.")
                : Result.Success(money));

        DetectAnomaliesUseCase useCase = CreateUseCase(expenseRepo, alertRepo: alertRepo, converter: converter);

        Result result = await useCase.ExecuteAsync(_anchor);

        Assert.True(result.IsSuccess);
        AnomalyAlert persisted = Assert.Single(alertRepo.Alerts);
        Assert.Equal(existing.Id, persisted.Id);
        Assert.Equal("Existing", persisted.Message);
        Assert.Equal(AnomalySeverity.Warning, persisted.Severity);

        AnomalyDetectionResult detection = Assert.IsType<AnomalyDetectionResult>(result.Value);
        Assert.True(detection.HasMissingRates);
    }

    [Fact]
    public async Task ExecuteAsync_MixedCurrencyHistory_NormalizesToDopBeforeScoring()
    {
        var categoryId = Guid.NewGuid();
        FakeExpenseRepository expenseRepo = new();

        // Same worked-example values, but every other entry is booked in USD
        // at half the DOP amount; the converter below doubles USD -> DOP, so
        // the normalized history is identical to the pure-DOP worked example.
        (DateOnly Date, decimal Amount, Currency Currency)[] history =
        [
            (new DateOnly(2025, 12, 3), 100m, Currency.DOP),
            (new DateOnly(2025, 12, 20), 50m, Currency.USD),   // -> 100 DOP
            (new DateOnly(2026, 1, 3), 110m, Currency.DOP),
            (new DateOnly(2026, 1, 15), 45m, Currency.USD),    // -> 90 DOP
            (new DateOnly(2026, 1, 28), 100m, Currency.DOP),
            (new DateOnly(2026, 2, 3), 60m, Currency.USD),     // -> 120 DOP
            (new DateOnly(2026, 2, 15), 80m, Currency.DOP),
            (new DateOnly(2026, 2, 26), 100m, Currency.DOP),
        ];

        foreach ((DateOnly date, decimal amount, Currency currency) in history)
        {
            await expenseRepo.AddAsync(CreateExpense(categoryId, date, amount, currency));
        }

        Expense candidate = CreateExpense(categoryId, new DateOnly(2026, 3, 5), 145m, Currency.DOP);
        await expenseRepo.AddAsync(candidate);

        FakeCurrencyConverter converter = new(onConvert: (money, _) =>
            money.Currency == Currency.USD
                ? Result.Success(Money.Create(money.Amount * 2m, Currency.DOP))
                : Result.Success(money));

        FakeAnomalyAlertRepository alertRepo = new();
        DetectAnomaliesUseCase useCase = CreateUseCase(expenseRepo, alertRepo: alertRepo, converter: converter);

        Result result = await useCase.ExecuteAsync(_anchor);

        Assert.True(result.IsSuccess);
        Assert.True(converter.ConvertCallCount > 0);
        AnomalyDetectionResult detection = Assert.IsType<AnomalyDetectionResult>(result.Value);
        AnomalyItemDto item = Assert.Single(detection.Items);
        Assert.Equal(candidate.Id, item.ExpenseId);
        Assert.Equal(AnomalySeverity.Warning, item.Severity);
        Assert.Equal(100m, item.MedianDop);
    }
}
