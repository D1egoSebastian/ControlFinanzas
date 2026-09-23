using ControlDeFinanzasIA.Application.Abstractions.Llm;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Shared builder for a real <see cref="GetDashboardInsightsQuery"/> wired
/// entirely from fakes (F-014, slice S8a, D-13). Centralizes the ctor-arity
/// change of <see cref="ControlDeFinanzasIA.UI.ViewModels.DashboardViewModel"/>
/// so its three test call sites (<c>DashboardViewModelTests</c>,
/// <c>NavigationServiceTests</c>, <c>ShellViewModelTests</c>) share one
/// wiring instead of duplicating the nine-dependency composite by hand.
/// Defaults to an AI stack that is NOT configured (zero <see cref="ILLMClient"/>
/// calls), matching every fixture that does not care about narration.
/// </summary>
public static class DashboardInsightsQueryBuilder
{
    /// <summary>Builds a real <see cref="GetDashboardInsightsQuery"/> from the given fakes.</summary>
    public static GetDashboardInsightsQuery Build(
        FakeExpenseRepository expenseRepo,
        FakeIncomeRepository incomeRepo,
        FakeCurrencyConverter converter,
        FakeClock clock,
        FakeBudgetRepository? budgetRepo = null,
        FakeCategoryRepository? categoryRepo = null,
        FakeAnomalyAlertRepository? alertRepo = null,
        FakeLlmClientFactory? llmFactory = null,
        FakeSettingsStore? settings = null)
    {
        ArgumentNullException.ThrowIfNull(expenseRepo);
        ArgumentNullException.ThrowIfNull(incomeRepo);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(clock);

        budgetRepo ??= new FakeBudgetRepository();
        categoryRepo ??= new FakeCategoryRepository();
        alertRepo ??= new FakeAnomalyAlertRepository();
        llmFactory ??= new FakeLlmClientFactory
        {
            ResolveResult = LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey),
        };
        settings ??= new FakeSettingsStore();

        var messageFormatter = new FakeAnomalyMessageFormatter();
        var prompts = new FakePromptProvider();
        var cache = new DashboardNarrationCache();

        var forecast = new GetMonthEndForecastUseCase(expenseRepo, budgetRepo, converter, clock);
        var detect = new DetectAnomaliesUseCase(
            expenseRepo, categoryRepo, alertRepo, converter, messageFormatter, clock,
            NullLogger<DetectAnomaliesUseCase>.Instance);
        var acknowledge = new AcknowledgeAnomalyUseCase(alertRepo, clock);
        var monthSummary = new GetMonthSummaryQuery(expenseRepo, incomeRepo, converter);
        var categoryBreakdown = new GetCategoryBreakdownQuery(expenseRepo, converter);
        var budgetVsActual = new GetBudgetVsActualQuery(budgetRepo, expenseRepo, categoryRepo, converter);
        var insights = new GetInsightsUseCase(
            settings, llmFactory, prompts, cache, NullLogger<GetInsightsUseCase>.Instance);
        var anomalyNarrative = new GetAnomalyNarrativeUseCase(
            settings, llmFactory, prompts, cache, NullLogger<GetAnomalyNarrativeUseCase>.Instance);

        return new GetDashboardInsightsQuery(
            forecast,
            detect,
            acknowledge,
            monthSummary,
            categoryBreakdown,
            budgetVsActual,
            insights,
            anomalyNarrative,
            NullLogger<GetDashboardInsightsQuery>.Instance);
    }
}
