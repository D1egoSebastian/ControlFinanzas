using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetInsights;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UseCases.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetInsightsUseCase"/> (F-014, slice S6, R-4,
/// R-6, D-8). Copies <c>CategorizeExpenseUseCase</c>'s guard skeleton, with
/// the one deliberate difference: every non-happy path is
/// <c>Result.Success</c> with a fallback <see cref="NarrativeStatus"/>,
/// never <c>Result.Failure</c>.
/// </summary>
public sealed class GetInsightsUseCaseTests
{
    private static InsightFacts CreateFacts(decimal expensesDop = 1000m, decimal incomesDop = 500m) =>
        new(
            Month: new DateOnly(2026, 3, 1),
            ExpensesDop: expensesDop,
            IncomesDop: incomesDop,
            NetBalanceDop: incomesDop - expensesDop,
            ExpensesDeltaPercent: 12.5m,
            TopCategories:
            [
                new CategorySlice(Guid.NewGuid(), "Supermercado", 600m),
                new CategorySlice(Guid.NewGuid(), "Transporte", 400m),
            ],
            TopBudgetLines:
            [
                new BudgetVsActualLine(Guid.NewGuid(), Guid.NewGuid(), "Alimentacion", 800m, 600m, 75m, BudgetStatus.Ok),
            ],
            ProjectedDop: 1200m,
            BudgetTotalDop: 1500m,
            AnomalyCount: 2,
            HasMissingRates: false);

    private static (
        FakeSettingsStore Settings,
        FakeLlmClientFactory Factory,
        FakeLlmClient Client,
        FakePromptProvider Prompts,
        DashboardNarrationCache Cache,
        RecordingLogger<GetInsightsUseCase> Logger,
        GetInsightsUseCase UseCase) CreateSut(bool configured = true)
    {
        var settings = new FakeSettingsStore { AppSettings = AppSettings.Defaults with { AiApiKey = "sk-test" } };
        var client = new FakeLlmClient { ResponseToReturn = new LlmResponse("Texto generado por el modelo.", 42, 10) };
        var factory = new FakeLlmClientFactory
        {
            ResolveResult = configured
                ? LlmClientResolution.Configured(client)
                : LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey),
        };
        var prompts = new FakePromptProvider();
        var cache = new DashboardNarrationCache();
        var logger = new RecordingLogger<GetInsightsUseCase>();
        var useCase = new GetInsightsUseCase(settings, factory, prompts, cache, logger);

        return (settings, factory, client, prompts, cache, logger, useCase);
    }

    [Fact]
    public async Task ExecuteAsync_NotConfigured_ReturnsSuccessWithNotConfiguredAndMakesZeroCalls()
    {
        (_, FakeLlmClientFactory factory, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut(configured: false);

        Result result = await useCase.ExecuteAsync(CreateFacts());

        Assert.True(result.IsSuccess);
        InsightsNarrative narrative = Assert.IsType<InsightsNarrative>(result.Value);
        Assert.Equal(NarrativeStatus.NotConfigured, narrative.Status);
        Assert.Null(narrative.Text);
        Assert.Equal(0, client.CompleteCallCount);
        Assert.Single(factory.ResolveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ExpensesAndIncomesBothZero_IsSkippedBeforeReadingSettings()
    {
        (_, FakeLlmClientFactory factory, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut();

        Result result = await useCase.ExecuteAsync(CreateFacts(expensesDop: 0m, incomesDop: 0m));

        Assert.True(result.IsSuccess);
        InsightsNarrative narrative = Assert.IsType<InsightsNarrative>(result.Value);
        Assert.Equal(NarrativeStatus.Skipped, narrative.Status);
        Assert.Equal(0, client.CompleteCallCount);
        Assert.Empty(factory.ResolveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_LlmExceptionThrown_ReturnsSuccessWithUnavailableAndLogsWarningWithCorrelationId()
    {
        (_, _, FakeLlmClient client, _, _, RecordingLogger<GetInsightsUseCase> logger, GetInsightsUseCase useCase) = CreateSut();
        client.ExceptionToThrow = new LlmException(LlmFailureKind.Timeout, "attempt timeout");

        Result result = await useCase.ExecuteAsync(CreateFacts());

        Assert.True(result.IsSuccess);
        InsightsNarrative narrative = Assert.IsType<InsightsNarrative>(result.Value);
        Assert.Equal(NarrativeStatus.Unavailable, narrative.Status);
        Assert.Null(narrative.Text);
        Assert.True(logger.HasWarning);
        Assert.Contains(logger.Properties, p => string.Equals(p.Key, "CorrelationId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_UnparseableEmptyReply_ReturnsSuccessWithUnavailable()
    {
        (_, _, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut();
        client.ResponseToReturn = new LlmResponse("   ", 5, 5);

        Result result = await useCase.ExecuteAsync(CreateFacts());

        Assert.True(result.IsSuccess);
        InsightsNarrative narrative = Assert.IsType<InsightsNarrative>(result.Value);
        Assert.Equal(NarrativeStatus.Unavailable, narrative.Status);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancellation_RethrowsOperationCanceledException()
    {
        (_, _, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut();
        using var cts = new CancellationTokenSource();
        client.OnCompleteAsync = (_, _) =>
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return Task.FromResult(new LlmResponse("unreachable", 0, 0));
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => useCase.ExecuteAsync(CreateFacts(), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_CapsMaxTokensAt400()
    {
        (_, _, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut();

        await useCase.ExecuteAsync(CreateFacts());

        LlmRequest request = Assert.Single(client.CompleteRequests);
        Assert.True(request.MaxTokens <= 400);
    }

    [Fact]
    public async Task ExecuteAsync_ResponseWithFencesAndExtraNewlines_CleansUpTheOutput()
    {
        (_, _, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut();
        client.ResponseToReturn = new LlmResponse(
            "```\nEste mes gastaste bien.\n\n\n\nEl balance es positivo.\n```",
            10,
            10);

        Result result = await useCase.ExecuteAsync(CreateFacts());

        InsightsNarrative narrative = Assert.IsType<InsightsNarrative>(result.Value);
        Assert.Equal(NarrativeStatus.Generated, narrative.Status);
        Assert.NotNull(narrative.Text);
        Assert.DoesNotContain("```", narrative.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", narrative.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LongResponse_IsCappedAt800Characters()
    {
        (_, _, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut();
        client.ResponseToReturn = new LlmResponse(new string('a', 2000), 10, 10);

        Result result = await useCase.ExecuteAsync(CreateFacts());

        InsightsNarrative narrative = Assert.IsType<InsightsNarrative>(result.Value);
        Assert.NotNull(narrative.Text);
        Assert.True(narrative.Text!.Length <= 800);
    }

    [Fact]
    public async Task ExecuteAsync_SecondIdenticalLoad_HitsCacheWithZeroExtraCalls()
    {
        (_, _, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut();
        InsightFacts facts = CreateFacts();

        await useCase.ExecuteAsync(facts);
        Result second = await useCase.ExecuteAsync(facts);

        Assert.Equal(1, client.CompleteCallCount);
        InsightsNarrative narrative = Assert.IsType<InsightsNarrative>(second.Value);
        Assert.Equal(NarrativeStatus.Generated, narrative.Status);
    }

    [Fact]
    public async Task ExecuteAsync_Fallback_IsNeverCached()
    {
        (_, _, FakeLlmClient client, _, _, _, GetInsightsUseCase useCase) = CreateSut();
        client.ExceptionToThrow = new LlmException(LlmFailureKind.ServerError, "500");
        InsightFacts facts = CreateFacts();

        await useCase.ExecuteAsync(facts);
        client.ExceptionToThrow = null;
        client.ResponseToReturn = new LlmResponse("Texto valido.", 5, 5);
        await useCase.ExecuteAsync(facts);

        Assert.Equal(2, client.CompleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_RenderedPromptContainsExactSuppliedFigures_NoNumberIsIntroduced()
    {
        (_, _, FakeLlmClient client, FakePromptProvider prompts, _, _, GetInsightsUseCase useCase) = CreateSut();
        prompts.Templates[PromptNames.GenerateInsightsUser] = "FACTS: {{facts}}";
        InsightFacts facts = CreateFacts(expensesDop: 4321.99m, incomesDop: 9999.01m);

        await useCase.ExecuteAsync(facts);

        LlmRequest request = Assert.Single(client.CompleteRequests);
        string userPrompt = request.Messages.Single(m => m.Role == LlmRole.User).Content;
        Assert.Contains("4,321.99", userPrompt, StringComparison.Ordinal);
        Assert.Contains("9,999.01", userPrompt, StringComparison.Ordinal);
    }
}
