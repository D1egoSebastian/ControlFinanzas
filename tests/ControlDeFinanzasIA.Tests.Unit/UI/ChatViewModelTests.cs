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
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Tests for <see cref="ChatViewModel"/> (F-015, Slice S5, R-9, R-10, R-11, R-12, R-13, D-9). Runs
/// the real <see cref="AskFinanceChatUseCase"/> over hand-rolled fakes, matching the
/// <c>AskFinanceChatUseCaseTests</c>/Phase 14 precedent. Covers the user/assistant bubble flow, the
/// one-question-in-flight rule (D-11: Send is disabled while pending — Stop cancels, it does not
/// queue), Stop's cancellation note excluded from replay (S-6), the "not configured" banner with
/// zero calls (S-2), the AI-unavailable fallback, Copy (S-11), <c>OnNavigatedFrom</c> clearing an
/// in-flight send via the generation-token guard (S-12), and the figures panel matching
/// <see cref="ChatContextRenderer.FormatDop"/> of the snapshot's current-month totals.
/// </summary>
public sealed class ChatViewModelTests
{
    private static readonly DateOnly _today = new(2026, 9, 20);
    private static readonly DateOnly _currentMonthAnchor = new(2026, 9, 1);

    internal sealed record Sut(
        ChatViewModel ViewModel,
        FakeSettingsStore SettingsStore,
        FakeLlmClientFactory Factory,
        FakeLlmClient Client,
        FakeClipboardService Clipboard,
        FakeExpenseRepository ExpenseRepo,
        RecordingLogger<ChatViewModel> Logger);

    internal static Sut CreateSut(AppSettings? settings = null)
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
        prompts.Templates[PromptNames.ChatFinanceSystem] = "SYSTEM RULES\n{{context}}";

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
        var buildContextLogger = new RecordingLogger<BuildChatContextQuery>();

        var buildContext = new BuildChatContextQuery(
            monthSummary, categoryBreakdown, budgetVsActual, goals, debts, debtSchedule, upcomingPayments,
            converter, clock, buildContextLogger);

        var useCaseLogger = new RecordingLogger<AskFinanceChatUseCase>();
        var useCase = new AskFinanceChatUseCase(settingsStore, factory, buildContext, prompts, useCaseLogger);

        var clipboard = new FakeClipboardService();
        var localizer = new FakeStringLocalizer<Strings>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ChatThinking"] = "Pensando…",
            ["ChatAiNotConfiguredBanner"] = "IA no configurada — configúrala en Configuración",
            ["ChatAiUnavailable"] = "IA no disponible. Intenta de nuevo.",
            ["ChatCancelledNote"] = "Pregunta detenida.",
            ["ChatQuestionRejected"] = "La pregunta debe tener entre 1 y 500 caracteres.",
            ["ChatPrivacyDisclosureFormat"] = "Se envía un resumen a {0}.",
        });
        var vmLogger = new RecordingLogger<ChatViewModel>();

        var viewModel = new ChatViewModel(useCase, clipboard, localizer, vmLogger);

        return new Sut(viewModel, settingsStore, factory, client, clipboard, expenseRepo, vmLogger);
    }

    private static async Task SeedCategoryExpenseAsync(FakeExpenseRepository repo, string name, decimal amount, DateOnly date)
    {
        var categoryId = Guid.NewGuid();
        repo.CategoryNames[categoryId] = name;
        await repo.AddAsync(Expense.Create(
            categoryId, date, Money.Create(amount, Currency.DOP), name, null, DateTime.UtcNow));
    }

    private static async Task ActivateAsync(ChatViewModel vm)
    {
        vm.OnNavigatedTo();
        await Task.Delay(1); // allow the fire-and-forget availability refresh to complete
    }

    // ------------------------------------------------------------------
    // Send: user + assistant bubbles, answered turns grow only on Answered
    // ------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_Answered_AppendsUserBubbleThenAssistantBubble()
    {
        Sut sut = await CreateActivatedSutAsync();

        sut.ViewModel.QuestionText = "¿Cuánto gasté en comida este mes?";
        await sut.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(2, sut.ViewModel.Messages.Count);
        Assert.Equal(ChatMessageRole.User, sut.ViewModel.Messages[0].Role);
        Assert.Equal("¿Cuánto gasté en comida este mes?", sut.ViewModel.Messages[0].Text);
        Assert.Equal(ChatMessageRole.Assistant, sut.ViewModel.Messages[1].Role);
        Assert.Equal(ChatMessageState.Answered, sut.ViewModel.Messages[1].State);
        Assert.Equal("Respuesta de prueba.", sut.ViewModel.Messages[1].Text);
        Assert.False(sut.ViewModel.Messages[1].IsNote);
        Assert.Equal(string.Empty, sut.ViewModel.QuestionText);
    }

    [Fact]
    public async Task SendAsync_Answered_ThenSecondQuestion_ReplaysThePriorExchange()
    {
        Sut sut = await CreateActivatedSutAsync();

        sut.ViewModel.QuestionText = "Primera pregunta";
        await sut.ViewModel.SendCommand.ExecuteAsync(null);

        sut.ViewModel.QuestionText = "Segunda pregunta";
        await sut.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(2, sut.Client.CompleteCallCount);
        IReadOnlyList<LlmMessage> secondMessages = sut.Client.CompleteRequests[1].Messages;
        Assert.Contains(secondMessages, m => m.Role == LlmRole.User && m.Content == "Primera pregunta");
        Assert.Contains(secondMessages, m => m.Role == LlmRole.Assistant && m.Content == "Respuesta de prueba.");
    }

    [Fact]
    public async Task SendAsync_Rejected_NotConfigured_Unavailable_AreNeverReplayed()
    {
        Sut sut = await CreateActivatedSutAsync();

        // Rejected: 501-char question.
        sut.ViewModel.QuestionText = new string('a', 501);
        await sut.ViewModel.SendCommand.ExecuteAsync(null);
        Assert.Equal(ChatMessageState.Rejected, sut.ViewModel.Messages[^1].State);

        // Unavailable: the fake client throws.
        sut.Client.ExceptionToThrow = new LlmException(LlmFailureKind.Timeout, "boom");
        sut.ViewModel.QuestionText = "¿Cuánto ahorré este año?";
        await sut.ViewModel.SendCommand.ExecuteAsync(null);
        Assert.Equal(ChatMessageState.Unavailable, sut.ViewModel.Messages[^1].State);
        sut.Client.ExceptionToThrow = null;

        // One final answered question — its replay must contain neither rejected nor unavailable turns.
        sut.ViewModel.QuestionText = "Pregunta final";
        await sut.ViewModel.SendCommand.ExecuteAsync(null);

        IReadOnlyList<LlmMessage> finalMessages = sut.Client.CompleteRequests[^1].Messages;
        Assert.DoesNotContain(finalMessages, m => m.Content == new string('a', 501));
        Assert.DoesNotContain(finalMessages, m => m.Content == "¿Cuánto ahorré este año?");
    }

    // ------------------------------------------------------------------
    // R-9 / D-11: one question in flight, Send disabled, Stop cancels
    // ------------------------------------------------------------------

    [Fact]
    public async Task SendCommand_SecondInvocationWhileSending_IsANoOp()
    {
        Sut sut = await CreateActivatedSutAsync();
        var gate = new TaskCompletionSource<LlmResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Client.OnCompleteAsync = (_, _) => gate.Task;

        sut.ViewModel.QuestionText = "Primera";
        Task firstSend = sut.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.True(sut.ViewModel.IsSending);
        Assert.False(sut.ViewModel.SendCommand.CanExecute(null));

        // A second invocation while sending must be a genuine no-op: the explicit IsSending guard
        // inside SendAsync returns immediately without awaiting anything, so this completes even
        // though the first call's gate is still open (R-9). Not awaited directly on purpose — if the
        // guard ever regressed to await the same never-completed gate, this would hang instead of
        // failing fast.
        sut.ViewModel.QuestionText = "Segunda";
        Task secondSend = sut.ViewModel.SendCommand.ExecuteAsync(null);
        Task completedFirst = await Task.WhenAny(secondSend, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(secondSend, completedFirst);
        await secondSend;

        Assert.Equal(1, sut.Client.CompleteCallCount);
        Assert.Equal(2, sut.ViewModel.Messages.Count);

        gate.SetResult(new LlmResponse("Respuesta de prueba.", 10, 10));
        await firstSend;
    }

    [Fact]
    public async Task Stop_CancelsInFlightQuestion_ShowsCancellationNoteAndIsExcludedFromReplay()
    {
        Sut sut = await CreateActivatedSutAsync();
        var gate = new TaskCompletionSource<LlmResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Client.OnCompleteAsync = (_, ct) =>
        {
            ct.Register(() => gate.TrySetCanceled(ct));
            return gate.Task;
        };

        sut.ViewModel.QuestionText = "Pregunta que se detiene";
        Task send = sut.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.True(sut.ViewModel.StopCommand.CanExecute(null));
        sut.ViewModel.StopCommand.Execute(null);
        await send;

        Assert.Equal(ChatMessageState.Cancelled, sut.ViewModel.Messages[^1].State);
        Assert.Equal("Pregunta detenida.", sut.ViewModel.Messages[^1].Text);
        Assert.True(sut.ViewModel.Messages[^1].IsNote); // a cancellation note, never an error/failure bubble
        Assert.False(sut.ViewModel.IsSending);

        // The cancelled turn must never be replayed: send a fresh, real question and inspect its request.
        sut.ViewModel.QuestionText = "Pregunta normal";
        await sut.ViewModel.SendCommand.ExecuteAsync(null);
        IReadOnlyList<LlmMessage> nextMessages = sut.Client.CompleteRequests[^1].Messages;
        Assert.DoesNotContain(nextMessages, m => m.Content == "Pregunta que se detiene");
    }

    // ------------------------------------------------------------------
    // S-2 / R-12: not configured
    // ------------------------------------------------------------------

    [Fact]
    public async Task NotConfigured_ShowsBannerAndDisablesSend_WithZeroCalls()
    {
        Sut sut = CreateSut();
        sut.Factory.ResolveResult = LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey);

        await ActivateAsync(sut.ViewModel);

        Assert.True(sut.ViewModel.ShowNotConfiguredBanner);
        Assert.False(sut.ViewModel.IsInputEnabled);

        sut.ViewModel.QuestionText = "¿Cuánto gasté?";
        Assert.False(sut.ViewModel.SendCommand.CanExecute(null));
        Assert.Equal(0, sut.Client.CompleteCallCount);

        // Only the availability check itself calls Resolve; SendCommand never even attempted.
        Assert.Single(sut.Factory.ResolveCalls);
    }

    // ------------------------------------------------------------------
    // S-11: Copy sends the exact displayed text
    // ------------------------------------------------------------------

    [Fact]
    public async Task CopyAsync_SendsExactDisplayedBubbleTextToClipboard()
    {
        Sut sut = await CreateActivatedSutAsync();

        sut.ViewModel.QuestionText = "¿Cuánto gasté en comida?";
        await sut.ViewModel.SendCommand.ExecuteAsync(null);

        ChatMessageItem assistantBubble = sut.ViewModel.Messages[1];
        Assert.True(sut.ViewModel.CopyCommand.CanExecute(assistantBubble));
        await sut.ViewModel.CopyCommand.ExecuteAsync(assistantBubble);

        Assert.Equal(assistantBubble.Text, sut.Clipboard.LastText);
    }

    // ------------------------------------------------------------------
    // S-12: OnNavigatedFrom mid-flight cancels, clears, drops the late completion
    // ------------------------------------------------------------------

    [Fact]
    public async Task OnNavigatedFrom_MidFlight_CancelsClearsAndDropsLateCompletion()
    {
        Sut sut = await CreateActivatedSutAsync();
        var gate = new TaskCompletionSource<LlmResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.Client.OnCompleteAsync = (_, ct) =>
        {
            ct.Register(() => gate.TrySetCanceled(ct));
            return gate.Task;
        };

        sut.ViewModel.QuestionText = "Pregunta en vuelo";
        Task send = sut.ViewModel.SendCommand.ExecuteAsync(null);

        sut.ViewModel.OnNavigatedFrom();
        await send;

        Assert.Empty(sut.ViewModel.Messages);
        Assert.False(sut.ViewModel.IsSending);

        // A late completion arriving after the reset (racing the cancellation) must never render.
        gate.TrySetResult(new LlmResponse("Respuesta tardía.", 1, 1));
        await Task.Delay(1);
        Assert.Empty(sut.ViewModel.Messages);
    }

    // ------------------------------------------------------------------
    // R-10: figures panel matches ChatContextRenderer.FormatDop
    // ------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_Answered_FiguresPanelMatchesFormatDopOfCurrentMonthTotals()
    {
        Sut sut = await CreateActivatedSutAsync();
        await SeedCategoryExpenseAsync(sut.ExpenseRepo, "Comida", 12_345.67m, _currentMonthAnchor);

        sut.ViewModel.QuestionText = "¿Cuánto gasté en comida este mes?";
        await sut.ViewModel.SendCommand.ExecuteAsync(null);

        var culture = System.Globalization.CultureInfo.GetCultureInfo(SupportedLocales.SpanishDominican);
        Assert.Equal(ChatContextRenderer.FormatDop(12_345.67m, culture), sut.ViewModel.FiguresExpenses);
        Assert.Equal(ChatContextRenderer.FormatDop(-12_345.67m, culture), sut.ViewModel.FiguresNet);
        Assert.Contains(sut.ViewModel.FigureLines, line =>
            line.Name == "Comida" && line.AmountDisplay == ChatContextRenderer.FormatDop(12_345.67m, culture));
    }

    [Fact]
    public void ChatMessageItem_IsUser_IsTrueOnlyForTheUsersOwnBubble()
    {
        Assert.True(ChatMessageItem.User("hola").IsUser);
        Assert.False(ChatMessageItem.Pending("...").IsUser);
    }

    private static async Task<Sut> CreateActivatedSutAsync(AppSettings? settings = null)
    {
        Sut sut = CreateSut(settings);
        await ActivateAsync(sut.ViewModel);
        return sut;
    }
}
