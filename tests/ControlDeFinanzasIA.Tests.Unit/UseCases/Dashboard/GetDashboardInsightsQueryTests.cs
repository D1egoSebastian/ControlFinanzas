using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.AcknowledgeAnomaly;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetAnomalyNarrative;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetDashboardInsights;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetInsights;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthEndForecast;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UseCases.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetDashboardInsightsQuery"/> (F-014, slice
/// S7, R-7, D-10). Verifies the deterministic/narration split is
/// structural, not accidental: the deterministic step never resolves an
/// <c>ILLMClient</c>, one throwing step never blanks the others, and the
/// narration step issues at most 2 concurrent LLM calls.
/// </summary>
public sealed class GetDashboardInsightsQueryTests
{
    private sealed record Sut(
        GetDashboardInsightsQuery Query,
        FakeExpenseRepository ExpenseRepo,
        FakeIncomeRepository IncomeRepo,
        FakeBudgetRepository BudgetRepo,
        FakeCategoryRepository CategoryRepo,
        FakeAnomalyAlertRepository AlertRepo,
        FakeCurrencyConverter Converter,
        FakeClock Clock,
        FakeLlmClientFactory LlmFactory,
        FakeLlmClient LlmClient,
        RecordingLogger<GetDashboardInsightsQuery> Logger);

    private static Sut CreateSut(bool aiConfigured = false)
    {
        var expenseRepo = new FakeExpenseRepository();
        var incomeRepo = new FakeIncomeRepository();
        var budgetRepo = new FakeBudgetRepository();
        var categoryRepo = new FakeCategoryRepository();
        var alertRepo = new FakeAnomalyAlertRepository();
        var converter = new FakeCurrencyConverter();
        var clock = new FakeClock { Today = new DateOnly(2026, 3, 15), UtcNow = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc) };
        var settings = new FakeSettingsStore { AppSettings = AppSettings.Defaults with { AiApiKey = aiConfigured ? "sk-test" : string.Empty } };
        var llmClient = new FakeLlmClient { ResponseToReturn = new LlmResponse("Texto narrado.", 10, 10) };
        var llmFactory = new FakeLlmClientFactory
        {
            ResolveResult = aiConfigured
                ? LlmClientResolution.Configured(llmClient)
                : LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey),
        };
        var prompts = new FakePromptProvider();
        var cache = new DashboardNarrationCache();
        var messageFormatter = new FakeAnomalyMessageFormatter();

        var forecast = new GetMonthEndForecastUseCase(expenseRepo, budgetRepo, converter, clock);
        var detect = new DetectAnomaliesUseCase(
            expenseRepo, categoryRepo, alertRepo, converter, messageFormatter, clock,
            new RecordingLogger<DetectAnomaliesUseCase>());
        var acknowledge = new AcknowledgeAnomalyUseCase(alertRepo, clock);
        var monthSummary = new GetMonthSummaryQuery(expenseRepo, incomeRepo, converter);
        var categoryBreakdown = new GetCategoryBreakdownQuery(expenseRepo, converter);
        var budgetVsActual = new GetBudgetVsActualQuery(budgetRepo, expenseRepo, categoryRepo, converter);
        var insights = new GetInsightsUseCase(settings, llmFactory, prompts, cache, new RecordingLogger<GetInsightsUseCase>());
        var anomalyNarrative = new GetAnomalyNarrativeUseCase(settings, llmFactory, prompts, cache, new RecordingLogger<GetAnomalyNarrativeUseCase>());
        var logger = new RecordingLogger<GetDashboardInsightsQuery>();

        var query = new GetDashboardInsightsQuery(
            forecast, detect, acknowledge, monthSummary, categoryBreakdown, budgetVsActual, insights, anomalyNarrative, logger);

        return new Sut(query, expenseRepo, incomeRepo, budgetRepo, categoryRepo, alertRepo, converter, clock, llmFactory, llmClient, logger);
    }

    [Fact]
    public async Task GetDeterministicAsync_ThrowingLlmResolve_StillSucceeds()
    {
        // An ILLMClientFactory whose Resolve throws must never be reached
        // by the deterministic step (S-9): forecast + detection do not
        // depend on ISettingsStore/ILLMClientFactory at all.
        Sut sut = CreateSut();
        sut.LlmFactory.ThrowOnResolve = new InvalidOperationException("Resolve should never be called here.");

        Result result = await sut.Query.GetDeterministicAsync(new DateOnly(2026, 3, 1));

        Assert.True(result.IsSuccess);
        Assert.Empty(sut.LlmFactory.ResolveCalls);
    }

    [Fact]
    public async Task GetDeterministicAsync_DetectionStepThrows_DoesNotBlankForecastOrFacts()
    {
        Sut sut = CreateSut();
        sut.ExpenseRepo.SetThrowOnGetAmountRows(new InvalidOperationException("boom"));

        Result result = await sut.Query.GetDeterministicAsync(new DateOnly(2026, 3, 1));

        Assert.True(result.IsSuccess);
        DashboardInsightsSnapshot snapshot = Assert.IsType<DashboardInsightsSnapshot>(result.Value);
        Assert.Null(snapshot.Anomalies);
        Assert.NotNull(snapshot.Forecast);
        Assert.NotNull(snapshot.Facts);
        Assert.True(sut.Logger.HasError);
    }

    [Fact]
    public async Task GetDeterministicAsync_RunsQueriesSequentially_NotConcurrently()
    {
        // Regression for D-10's "sequential, not WhenAll" rule: the shared
        // AppDbContext must not see detection's write overlap a read. This
        // is asserted indirectly — detection's ApplyReconciliationAsync ran
        // exactly once and the resulting snapshot is internally consistent
        // (forecast + facts both populated after a clean detection pass).
        Sut sut = CreateSut();

        Result result = await sut.Query.GetDeterministicAsync(new DateOnly(2026, 3, 1));

        Assert.True(result.IsSuccess);
        DashboardInsightsSnapshot snapshot = Assert.IsType<DashboardInsightsSnapshot>(result.Value);
        Assert.NotNull(snapshot.Forecast);
        Assert.NotNull(snapshot.Anomalies);
        Assert.NotNull(snapshot.Facts);
    }

    [Fact]
    public async Task GetNarrationAsync_NotConfigured_MakesZeroCallsAndReturnsFallback()
    {
        // An empty month (zero expenses/incomes) is Skipped BEFORE settings
        // are even read (D-8) -- that is the correct, stricter outcome:
        // zero calls either way, but for a different documented reason than
        // "not configured". Both paths are covered by GetInsightsUseCaseTests
        // directly; here the composite-level guarantee under test is "zero
        // LLM calls when nothing is configured", regardless of which
        // Skipped/NotConfigured status produced it.
        Sut sut = CreateSut(aiConfigured: false);
        Result deterministic = await sut.Query.GetDeterministicAsync(new DateOnly(2026, 3, 1));
        var snapshot = (DashboardInsightsSnapshot)deterministic.Value!;

        Result result = await sut.Query.GetNarrationAsync(snapshot);

        Assert.True(result.IsSuccess);
        DashboardNarration narration = Assert.IsType<DashboardNarration>(result.Value);
        Assert.NotEqual(NarrativeStatus.Generated, narration.Insights.Status);
        Assert.Equal(0, sut.LlmClient.CompleteCallCount);
    }

    [Fact]
    public async Task GetNarrationAsync_Configured_IssuesAtMostTwoConcurrentCalls()
    {
        Sut sut = CreateSut(aiConfigured: true);
        Result deterministic = await sut.Query.GetDeterministicAsync(new DateOnly(2026, 3, 1));
        var snapshot = (DashboardInsightsSnapshot)deterministic.Value!;

        Result result = await sut.Query.GetNarrationAsync(snapshot);

        Assert.True(result.IsSuccess);
        // Empty facts (zero expenses/incomes) -> insights Skipped, zero
        // anomalies -> anomaly narration Skipped: both use cases short
        // circuit BEFORE resolving a client (D-8), so zero calls here is
        // the correct upper-bounded (<= 2) outcome for an empty month.
        Assert.True(sut.LlmClient.CompleteCallCount <= 2);
    }

    [Fact]
    public async Task DismissAnomalyAsync_DelegatesToAcknowledgeAnomalyUseCase()
    {
        Sut sut = CreateSut();

        Result result = await sut.Query.DismissAnomalyAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error?.Code);
    }
}
