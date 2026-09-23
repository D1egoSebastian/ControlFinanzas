using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetAnomalyNarrative;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UseCases.Dashboard;

/// <summary>
/// Unit tests for <see cref="GetAnomalyNarrativeUseCase"/> (F-014, slice
/// S6, R-5, R-6, D-8, D-9). One batched call narrates the top 5 anomalies
/// by score; every non-happy path is success-shaped.
/// </summary>
public sealed class GetAnomalyNarrativeUseCaseTests
{
    private static AnomalyItemDto CreateItem(string category = "Supermercado") =>
        new(
            AlertId: Guid.NewGuid(),
            ExpenseId: Guid.NewGuid(),
            Date: new DateOnly(2026, 3, 14),
            CategoryName: category,
            Description: "Compra quincenal",
            AmountDop: 12500m,
            MedianDop: 2300m,
            RatioToMedian: 5.43m,
            Severity: AnomalySeverity.Warning,
            Message: "Plantilla determinística.");

    private static (
        FakeSettingsStore Settings,
        FakeLlmClientFactory Factory,
        FakeLlmClient Client,
        FakePromptProvider Prompts,
        DashboardNarrationCache Cache,
        RecordingLogger<GetAnomalyNarrativeUseCase> Logger,
        GetAnomalyNarrativeUseCase UseCase) CreateSut(bool configured = true)
    {
        var settings = new FakeSettingsStore { AppSettings = AppSettings.Defaults with { AiApiKey = "sk-test" } };
        var client = new FakeLlmClient { ResponseToReturn = new LlmResponse("[{\"id\":1,\"message\":\"Gasto inusual.\"}]", 20, 10) };
        var factory = new FakeLlmClientFactory
        {
            ResolveResult = configured
                ? LlmClientResolution.Configured(client)
                : LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey),
        };
        var prompts = new FakePromptProvider();
        var cache = new DashboardNarrationCache();
        var logger = new RecordingLogger<GetAnomalyNarrativeUseCase>();
        var useCase = new GetAnomalyNarrativeUseCase(settings, factory, prompts, cache, logger);

        return (settings, factory, client, prompts, cache, logger, useCase);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyList_ReturnsSkippedAndMakesZeroCalls()
    {
        (_, FakeLlmClientFactory factory, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();

        Result result = await useCase.ExecuteAsync([]);

        Assert.True(result.IsSuccess);
        AnomalyNarration narration = Assert.IsType<AnomalyNarration>(result.Value);
        Assert.Equal(NarrativeStatus.Skipped, narration.Status);
        Assert.Empty(narration.MessagesByAlertId);
        Assert.Equal(0, client.CompleteCallCount);
        Assert.Empty(factory.ResolveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_NotConfigured_ReturnsNotConfiguredAndMakesZeroCalls()
    {
        (_, _, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut(configured: false);

        Result result = await useCase.ExecuteAsync([CreateItem()]);

        Assert.True(result.IsSuccess);
        AnomalyNarration narration = Assert.IsType<AnomalyNarration>(result.Value);
        Assert.Equal(NarrativeStatus.NotConfigured, narration.Status);
        Assert.Empty(narration.MessagesByAlertId);
        Assert.Equal(0, client.CompleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingClient_ReturnsUnavailableAndLogsWarningWithCorrelationId()
    {
        (_, _, FakeLlmClient client, _, _, RecordingLogger<GetAnomalyNarrativeUseCase> logger, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        client.ExceptionToThrow = new LlmException(LlmFailureKind.ServerError, "500");

        Result result = await useCase.ExecuteAsync([CreateItem()]);

        Assert.True(result.IsSuccess);
        AnomalyNarration narration = Assert.IsType<AnomalyNarration>(result.Value);
        Assert.Equal(NarrativeStatus.Unavailable, narration.Status);
        Assert.Empty(narration.MessagesByAlertId);
        Assert.True(logger.HasWarning);
        Assert.Contains(logger.Properties, p => string.Equals(p.Key, "CorrelationId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_MoreThanFiveAnomalies_OnlyNarratesTopFive()
    {
        (_, _, FakeLlmClient client, FakePromptProvider prompts, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        prompts.Templates[PromptNames.ExplainAnomaliesUser] = "{{anomalies}}";
        var items = Enumerable.Range(0, 7).Select(i => CreateItem($"Cat{i}")).ToList();
        client.OnCompleteAsync = (request, _) =>
        {
            string userPrompt = request.Messages.Single(m => m.Role == LlmRole.User).Content;
            return Task.FromResult(new LlmResponse(userPrompt, 0, 0));
        };

        await useCase.ExecuteAsync(items);

        LlmRequest sent = Assert.Single(client.CompleteRequests);
        string userPrompt = sent.Messages.Single(m => m.Role == LlmRole.User).Content;
        Assert.Contains("Cat0", userPrompt, StringComparison.Ordinal);
        Assert.Contains("Cat4", userPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Cat5", userPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Cat6", userPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_OrdinalToAlertIdMapNeverLeaksAGuidIntoThePrompt()
    {
        (_, _, FakeLlmClient client, FakePromptProvider prompts, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        prompts.Templates[PromptNames.ExplainAnomaliesUser] = "{{anomalies}}";
        AnomalyItemDto item = CreateItem();

        await useCase.ExecuteAsync([item]);

        LlmRequest request = Assert.Single(client.CompleteRequests);
        string userPrompt = request.Messages.Single(m => m.Role == LlmRole.User).Content;
        Assert.DoesNotContain(item.AlertId.ToString(), userPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(item.ExpenseId.ToString(), userPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ParsedReply_MapsOrdinalBackToTheOriginalAlertId()
    {
        (_, _, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        AnomalyItemDto item = CreateItem();
        client.ResponseToReturn = new LlmResponse("[{\"id\":1,\"message\":\"Gasto atípico en la categoría.\"}]", 20, 10);

        Result result = await useCase.ExecuteAsync([item]);

        AnomalyNarration narration = Assert.IsType<AnomalyNarration>(result.Value);
        Assert.Equal(NarrativeStatus.Generated, narration.Status);
        Assert.True(narration.MessagesByAlertId.ContainsKey(item.AlertId));
        Assert.Equal("Gasto atípico en la categoría.", narration.MessagesByAlertId[item.AlertId]);
    }

    [Fact]
    public async Task ExecuteAsync_UnparseableReply_ReturnsUnavailable()
    {
        (_, _, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        client.ResponseToReturn = new LlmResponse("not json at all", 5, 5);

        Result result = await useCase.ExecuteAsync([CreateItem()]);

        AnomalyNarration narration = Assert.IsType<AnomalyNarration>(result.Value);
        Assert.Equal(NarrativeStatus.Unavailable, narration.Status);
        Assert.Empty(narration.MessagesByAlertId);
    }

    [Fact]
    public async Task ExecuteAsync_PartiallyParseableBatch_KeepsOnlyResolvedIdsInTheMap()
    {
        (_, _, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        AnomalyItemDto item1 = CreateItem("Cat1");
        AnomalyItemDto item2 = CreateItem("Cat2");
        AnomalyItemDto item3 = CreateItem("Cat3");
        client.ResponseToReturn = new LlmResponse(
            "[{\"id\":1,\"message\":\"Uno.\"},{\"id\":2,\"message\":123},{\"id\":4,\"message\":\"Fuera de rango.\"}]",
            20,
            10);

        Result result = await useCase.ExecuteAsync([item1, item2, item3]);

        AnomalyNarration narration = Assert.IsType<AnomalyNarration>(result.Value);
        Assert.Equal(NarrativeStatus.Generated, narration.Status);
        Assert.Single(narration.MessagesByAlertId);
        Assert.Equal("Uno.", narration.MessagesByAlertId[item1.AlertId]);
        Assert.False(narration.MessagesByAlertId.ContainsKey(item2.AlertId));
        Assert.False(narration.MessagesByAlertId.ContainsKey(item3.AlertId));
    }

    [Fact]
    public async Task ExecuteAsync_SecondIdenticalLoad_HitsCacheWithZeroExtraCalls()
    {
        (_, _, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        AnomalyItemDto item = CreateItem();

        await useCase.ExecuteAsync([item]);
        Result second = await useCase.ExecuteAsync([item]);

        Assert.Equal(1, client.CompleteCallCount);
        AnomalyNarration narration = Assert.IsType<AnomalyNarration>(second.Value);
        Assert.Equal(NarrativeStatus.Generated, narration.Status);
        Assert.True(narration.MessagesByAlertId.ContainsKey(item.AlertId));
    }

    [Fact]
    public async Task ExecuteAsync_Fallback_IsNeverCached()
    {
        (_, _, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        AnomalyItemDto item = CreateItem();
        client.ExceptionToThrow = new LlmException(LlmFailureKind.Timeout, "timeout");

        await useCase.ExecuteAsync([item]);
        client.ExceptionToThrow = null;
        await useCase.ExecuteAsync([item]);

        Assert.Equal(2, client.CompleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancellation_RethrowsOperationCanceledException()
    {
        (_, _, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();
        using var cts = new CancellationTokenSource();
        client.OnCompleteAsync = (_, _) =>
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return Task.FromResult(new LlmResponse("unreachable", 0, 0));
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => useCase.ExecuteAsync([CreateItem()], cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_MaxTokensIsCappedAt600()
    {
        (_, _, FakeLlmClient client, _, _, _, GetAnomalyNarrativeUseCase useCase) = CreateSut();

        await useCase.ExecuteAsync([CreateItem()]);

        LlmRequest request = Assert.Single(client.CompleteRequests);
        Assert.True(request.MaxTokens <= 600);
    }
}
