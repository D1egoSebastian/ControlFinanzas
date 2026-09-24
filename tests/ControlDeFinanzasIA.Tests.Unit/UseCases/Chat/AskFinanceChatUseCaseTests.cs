using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Chat.AskFinanceChat;
using ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UseCases.Chat;

/// <summary>
/// Tests for <see cref="AskFinanceChatUseCase"/> (F-015, Slice S3, R-4,
/// R-5, R-6, R-7, R-8, D-5). Covers question-length rejection, the
/// "not configured" guard, the exact assembled message list, the
/// temperature/MaxTokens ceilings, history replay caps, every AI failure
/// mode collapsing to <see cref="ChatReplyStatus.Unavailable"/>, caller
/// cancellation, and that neither the question nor the API key ever reach
/// a log message.
/// </summary>
public sealed class AskFinanceChatUseCaseTests
{
    private static readonly DateOnly _today = new(2026, 9, 20);

    private sealed record Sut(
        AskFinanceChatUseCase UseCase,
        FakeSettingsStore SettingsStore,
        FakeLlmClientFactory Factory,
        FakeLlmClient Client,
        FakePromptProvider Prompts,
        RecordingLogger<AskFinanceChatUseCase> Logger);

    private static Sut CreateSut(AppSettings? settings = null)
    {
        var settingsStore = new FakeSettingsStore
        {
            AppSettings = settings ?? (AppSettings.Defaults with { AiApiKey = "sk-test" }),
        };

        var client = new FakeLlmClient
        {
            ResponseToReturn = new LlmResponse("Respuesta de prueba.", 10, 10),
        };

        var factory = new FakeLlmClientFactory
        {
            ResolveResult = LlmClientResolution.Configured(client),
        };

        var prompts = new FakePromptProvider();
        prompts.Templates[PromptNames.ChatFinanceSystem] =
            "SYSTEM RULES: answer only from given figures, else say you don't have the data.\n{{context}}";

        var logger = new RecordingLogger<AskFinanceChatUseCase>();

        var useCase = new AskFinanceChatUseCase(
            settingsStore, factory, CreateBuildChatContextQuery(), prompts, logger);

        return new Sut(useCase, settingsStore, factory, client, prompts, logger);
    }

    private static BuildChatContextQuery CreateBuildChatContextQuery()
    {
        var expenseRepo = new FakeExpenseRepository();
        var incomeRepo = new FakeIncomeRepository();
        var budgetRepo = new FakeBudgetRepository();
        var categoryRepo = new FakeCategoryRepository();
        var goalRepo = new FakeGoalRepository();
        var goalContributionRepo = new FakeGoalContributionRepository();
        var debtRepo = new FakeDebtRepository();
        var debtPaymentRepo = new FakeDebtPaymentRepository();
        var recurringRuleRepo = new FakeRecurringRuleRepository();
        var converter = new FakeCurrencyConverter();
        var clock = new FakeClock { Today = _today };

        var monthSummary = new GetMonthSummaryQuery(expenseRepo, incomeRepo, converter);
        var categoryBreakdown = new GetCategoryBreakdownQuery(expenseRepo, converter);
        var budgetVsActual = new GetBudgetVsActualQuery(budgetRepo, expenseRepo, categoryRepo, converter);
        var goals = new GetGoalsQuery(goalRepo, goalContributionRepo, clock);
        var debts = new GetDebtsQuery(debtRepo);
        var debtSchedule = new GetDebtScheduleQuery(debtRepo, debtPaymentRepo);
        var upcomingPayments = new GetUpcomingPaymentsQuery(debtPaymentRepo, debtRepo, recurringRuleRepo, clock);
        var logger = new RecordingLogger<BuildChatContextQuery>();

        return new BuildChatContextQuery(
            monthSummary, categoryBreakdown, budgetVsActual, goals, debts, debtSchedule, upcomingPayments,
            converter, clock, logger);
    }

    private static ChatReply ExecuteSuccessfully(Result result)
    {
        Assert.True(result.IsSuccess);
        return Assert.IsType<ChatReply>(result.Value);
    }

    // ------------------------------------------------------------------
    // S-3: oversized/empty question is rejected without a call
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_EmptyQuestion_RejectedWithoutAnyCall()
    {
        Sut sut = CreateSut();

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("   ", []));

        ChatReply reply = ExecuteSuccessfully(result);
        Assert.Equal(ChatReplyStatus.Rejected, reply.Status);
        Assert.Null(reply.Text);
        Assert.Equal(0, sut.Client.CompleteCallCount);
        Assert.Empty(sut.Factory.ResolveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_QuestionExactly500Chars_IsAccepted()
    {
        Sut sut = CreateSut();
        string question = new string('a', 500);

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand(question, []));

        ChatReply reply = ExecuteSuccessfully(result);
        Assert.Equal(ChatReplyStatus.Answered, reply.Status);
        Assert.Equal(1, sut.Client.CompleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_Question501Chars_RejectedWithoutTruncatingAndWithoutAnyCall()
    {
        Sut sut = CreateSut();
        string question = new string('a', 501);

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand(question, []));

        ChatReply reply = ExecuteSuccessfully(result);
        Assert.Equal(ChatReplyStatus.Rejected, reply.Status);
        Assert.Null(reply.Text);
        Assert.Equal(0, sut.Client.CompleteCallCount);
        Assert.Empty(sut.Factory.ResolveCalls);
    }

    // ------------------------------------------------------------------
    // S-2: not configured makes zero HTTP calls
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NotConfigured_ReturnsNotConfiguredWithZeroCalls()
    {
        Sut sut = CreateSut();
        sut.Factory.ResolveResult = LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey);

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []));

        ChatReply reply = ExecuteSuccessfully(result);
        Assert.Equal(ChatReplyStatus.NotConfigured, reply.Status);
        Assert.Null(reply.Text);
        Assert.Equal(0, sut.Client.CompleteCallCount);
    }

    // ------------------------------------------------------------------
    // S-8: assembled Messages list, grounding rule present verbatim
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(SupportedLocales.SpanishDominican)]
    [InlineData(SupportedLocales.English)]
    public async Task ExecuteAsync_Answered_AssembledMessagesHaveSystemThenUserWithGroundingRule(string locale)
    {
        Sut sut = CreateSut(AppSettings.Defaults with { AiApiKey = "sk-test", Locale = locale });

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []));

        ExecuteSuccessfully(result);
        Assert.Single(sut.Client.CompleteRequests);
        LlmRequest request = sut.Client.CompleteRequests[0];

        Assert.Equal(2, request.Messages.Count);
        Assert.Equal(LlmRole.System, request.Messages[0].Role);
        Assert.Contains(
            "answer only from given figures, else say you don't have the data",
            request.Messages[0].Content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("{{context}}", request.Messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("FINANCIAL CONTEXT", request.Messages[0].Content, StringComparison.Ordinal);

        Assert.Equal(LlmRole.User, request.Messages[1].Role);
        Assert.Equal("¿Cuánto gasté en comida?", request.Messages[1].Content);
    }

    // ------------------------------------------------------------------
    // R-7: Temperature and MaxTokens ceilings
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0.7, 0.3)]
    [InlineData(0.1, 0.1)]
    public async Task ExecuteAsync_Temperature_NeverExceedsCeiling(double settingsTemperature, double expected)
    {
        Sut sut = CreateSut(AppSettings.Defaults with { AiApiKey = "sk-test", AiTemperature = settingsTemperature });

        await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []));

        Assert.Equal(expected, sut.Client.CompleteRequests[0].Temperature);
    }

    [Theory]
    [InlineData(2000, 500)]
    [InlineData(200, 200)]
    public async Task ExecuteAsync_MaxTokens_NeverExceedsCeiling(int settingsMaxTokens, int expected)
    {
        Sut sut = CreateSut(AppSettings.Defaults with { AiApiKey = "sk-test", AiMaxTokensPerRequest = settingsMaxTokens });

        await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []));

        Assert.Equal(expected, sut.Client.CompleteRequests[0].MaxTokens);
    }

    // ------------------------------------------------------------------
    // S-4: history beyond 6 messages is trimmed, oldest dropped first
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public async Task ExecuteAsync_ReplayBeyond6Messages_KeepsOnlyLast3PairsOldestDroppedFirst(int priorTurnCount)
    {
        Sut sut = CreateSut();
        var priorTurns = Enumerable.Range(1, priorTurnCount)
            .Select(i => new ChatTurn($"Pregunta {i}", $"Respuesta {i}"))
            .ToList();

        await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Y ahora?", priorTurns));

        LlmRequest request = sut.Client.CompleteRequests[0];

        // System + 6 replay messages (3 pairs) + the new user question.
        Assert.Equal(8, request.Messages.Count);

        IReadOnlyList<LlmMessage> replay = request.Messages.Skip(1).Take(6).ToList();
        int totalReplayChars = replay.Sum(m => m.Content.Length);
        Assert.True(totalReplayChars <= AskFinanceChatUseCase.MaxReplayTotalChars);

        // Oldest dropped first: only the last 3 turns survive.
        Assert.Equal(LlmRole.User, replay[0].Role);
        Assert.Equal($"Pregunta {priorTurnCount - 2}", replay[0].Content);
        Assert.Equal(LlmRole.Assistant, replay[1].Role);
        Assert.Equal($"Respuesta {priorTurnCount - 2}", replay[1].Content);
        Assert.Equal($"Pregunta {priorTurnCount}", replay[4].Content);
        Assert.Equal($"Respuesta {priorTurnCount}", replay[5].Content);

        Assert.Equal(LlmRole.User, request.Messages[^1].Role);
        Assert.Equal("¿Y ahora?", request.Messages[^1].Content);
    }

    [Fact]
    public async Task ExecuteAsync_ReplayAssistantAnswerOver600Chars_IsCappedTo600Chars()
    {
        Sut sut = CreateSut();
        var priorTurns = new List<ChatTurn> { new("¿Pregunta larga?", new string('x', 700)) };

        await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Otra?", priorTurns));

        LlmRequest request = sut.Client.CompleteRequests[0];
        string replayedAnswer = request.Messages[2].Content;

        Assert.Equal(AskFinanceChatUseCase.MaxReplayAssistantChars, replayedAnswer.Length);
        Assert.EndsWith("…", replayedAnswer, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // S-7: AI failure never throws, always Unavailable, carries snapshot
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_LlmExceptionThrown_ReturnsUnavailableCarryingSnapshot()
    {
        Sut sut = CreateSut();
        sut.Client.ExceptionToThrow = new LlmException(LlmFailureKind.ServerError, "500");

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []));

        ChatReply reply = ExecuteSuccessfully(result);
        Assert.Equal(ChatReplyStatus.Unavailable, reply.Status);
        Assert.Null(reply.Text);
        Assert.NotNull(reply.Snapshot);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutLikeFailure_ReturnsUnavailableNeverThrows()
    {
        Sut sut = CreateSut();

        // Simulates the linked CTS firing its own CancelAfter(15s) timeout:
        // the client observes cancellation on ITS token while the caller's
        // ct is never cancelled, so the use case's guarded rethrow does not
        // fire and the broad catch converts it to Unavailable.
        sut.Client.OnCompleteAsync = (_, token) => throw new OperationCanceledException(token);

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []));

        ChatReply reply = ExecuteSuccessfully(result);
        Assert.Equal(ChatReplyStatus.Unavailable, reply.Status);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancellation_RethrowsOperationCanceledExceptionUnchanged()
    {
        Sut sut = CreateSut();
        sut.Client.ExceptionToThrow = new OperationCanceledException();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyResponseContent_ReturnsUnavailable()
    {
        Sut sut = CreateSut();
        sut.Client.ResponseToReturn = new LlmResponse("   ", 0, 0);

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []));

        ChatReply reply = ExecuteSuccessfully(result);
        Assert.Equal(ChatReplyStatus.Unavailable, reply.Status);
        Assert.Null(reply.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ReplyOver4000Chars_IsCappedTo4000Chars()
    {
        Sut sut = CreateSut();
        sut.Client.ResponseToReturn = new LlmResponse(new string('r', 4100), 0, 0);

        Result result = await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand("¿Cuánto gasté en comida?", []));

        ChatReply reply = ExecuteSuccessfully(result);
        Assert.Equal(ChatReplyStatus.Answered, reply.Status);
        Assert.Equal(AskFinanceChatUseCase.MaxReplyChars, reply.Text!.Length);
        Assert.EndsWith("…", reply.Text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // R-6/D-13 privacy: the question text and API key are never logged
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SentinelQuestionText_NeverAppearsInAnyLogMessageOrProperty()
    {
        const string sentinel = "SENTINEL-DO-NOT-LOG-tuna-casserole-budget-question";
        Sut sut = CreateSut(AppSettings.Defaults with { AiApiKey = "sk-SENTINEL-KEY-DO-NOT-LOG" });
        sut.Client.ExceptionToThrow = new LlmException(LlmFailureKind.ServerError, "500");

        await sut.UseCase.ExecuteAsync(new AskFinanceChatCommand(sentinel, []));

        foreach (string message in sut.Logger.Messages)
        {
            Assert.DoesNotContain(sentinel, message, StringComparison.Ordinal);
            Assert.DoesNotContain("sk-SENTINEL-KEY-DO-NOT-LOG", message, StringComparison.Ordinal);
        }

        foreach (KeyValuePair<string, object?> property in sut.Logger.Properties)
        {
            string? value = property.Value?.ToString();
            if (value is not null)
            {
                Assert.DoesNotContain(sentinel, value, StringComparison.Ordinal);
                Assert.DoesNotContain("sk-SENTINEL-KEY-DO-NOT-LOG", value, StringComparison.Ordinal);
            }
        }
    }

    // ------------------------------------------------------------------
    // GetAvailabilityAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetAvailabilityAsync_Configured_ReturnsTrueWithProviderName()
    {
        Sut sut = CreateSut();
        sut.Client.ProviderName = "Anthropic";

        ChatAvailability availability = await sut.UseCase.GetAvailabilityAsync();

        Assert.True(availability.IsConfigured);
        Assert.Equal("Anthropic", availability.ProviderName);
    }

    [Fact]
    public async Task GetAvailabilityAsync_NotConfigured_ReturnsFalseWithFallbackProviderName()
    {
        Sut sut = CreateSut(AppSettings.Defaults with { AiApiKey = string.Empty, AiProvider = AiProvider.OpenAI });
        sut.Factory.ResolveResult = LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey);

        ChatAvailability availability = await sut.UseCase.GetAvailabilityAsync();

        Assert.False(availability.IsConfigured);
        Assert.Equal("OpenAI", availability.ProviderName);
    }
}
