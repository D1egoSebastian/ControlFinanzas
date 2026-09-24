using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Chat.AskFinanceChat;
using ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// View-model for the finance chat screen (F-015, D-9): drives the send/stop/copy/reset flow
/// against <see cref="AskFinanceChatUseCase"/> and <see cref="IClipboardService"/>, following the
/// <c>ExpensesViewModel</c> suggestion-request generation-token precedent (F-013, slice 6, D-12)
/// without its debounce. Implements <see cref="INavigationAware"/> because
/// <c>NavigationService.GoBack</c> re-activates the same instance instead of resolving a new one
/// (D-7, R-9, S-12), so navigating away is the only reliable signal to clear the conversation.
/// </summary>
public sealed partial class ChatViewModel : ViewModelBase, INavigationAware
{
    private readonly AskFinanceChatUseCase _ask;
    private readonly IClipboardService _clipboard;
    private readonly IStringLocalizer<Strings> _localizer;
    private readonly ILogger<ChatViewModel> _logger;

    /// <summary>The only replay source for <see cref="SendAsync"/> — grows exclusively on Answered turns (R-5, D-9).</summary>
    private readonly List<ChatTurn> _answeredTurns = [];

    /// <summary>
    /// Monotonically increasing token that lets a superseded/late <see cref="SendAsync"/>
    /// completion detect it no longer owns the conversation (a Stop, a reset, or navigating away
    /// happened meanwhile) and discard its result instead of applying it (S-6, S-12).
    /// </summary>
    private int _generation;

    /// <summary>Cancellation source owned by the currently in-flight <see cref="SendAsync"/> invocation, if any.</summary>
    private CancellationTokenSource? _sendCts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatViewModel"/> class.
    /// </summary>
    public ChatViewModel(
        AskFinanceChatUseCase ask,
        IClipboardService clipboard,
        IStringLocalizer<Strings> localizer,
        ILogger<ChatViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(logger);

        _ask = ask;
        _clipboard = clipboard;
        _localizer = localizer;
        _logger = logger;
    }

    /// <summary>Gets the conversation transcript, cleared in-memory only (D2, S-12).</summary>
    public ObservableCollection<ChatMessageItem> Messages { get; } = [];

    /// <summary>Gets the exact-figure backup panel lines, rendered from the DTO, never from LLM text (R-10).</summary>
    public ObservableCollection<ChatFigureLine> FigureLines { get; } = [];

    /// <summary>Gets or sets the question text bound to the input box (R-4: capped at 500 chars by the view's <c>MaxLength</c>).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _questionText = string.Empty;

    /// <summary>Gets or sets a value indicating whether AI is configured, per <see cref="AskFinanceChatUseCase.GetAvailabilityAsync"/> (R-12).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNotConfiguredBanner))]
    [NotifyPropertyChangedFor(nameof(IsInputEnabled))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isAiConfigured;

    /// <summary>
    /// Gets or sets a value indicating whether the first availability check has completed, so the
    /// unconfigured banner never flickers on before the first check resolves.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNotConfiguredBanner))]
    private bool _isAvailabilityKnown;

    /// <summary>Gets or sets a value indicating whether one question is currently in flight (R-9).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputEnabled))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(StopCommand))]
    private bool _isSending;

    /// <summary>Gets or sets the persistent, localized privacy disclosure shown near the input (R-11).</summary>
    [ObservableProperty]
    private string _privacyDisclosure = string.Empty;

    /// <summary>Gets or sets the figures panel's current-month label.</summary>
    [ObservableProperty]
    private string _figuresMonthLabel = string.Empty;

    /// <summary>Gets or sets the figures panel's current-month expenses total, already formatted.</summary>
    [ObservableProperty]
    private string _figuresExpenses = string.Empty;

    /// <summary>Gets or sets the figures panel's current-month income total, already formatted.</summary>
    [ObservableProperty]
    private string _figuresIncomes = string.Empty;

    /// <summary>Gets or sets the figures panel's current-month net balance, already formatted.</summary>
    [ObservableProperty]
    private string _figuresNet = string.Empty;

    /// <summary>Gets or sets a value indicating whether the latest snapshot had a missing exchange rate (R-14).</summary>
    [ObservableProperty]
    private bool _figuresHaveMissingRates;

    /// <summary>Gets a value indicating whether the persistent "AI not configured" banner should show (D3, R-12).</summary>
    public bool ShowNotConfiguredBanner => IsAvailabilityKnown && !IsAiConfigured;

    /// <summary>Gets a value indicating whether the question input is enabled (R-9, R-12).</summary>
    public bool IsInputEnabled => IsAiConfigured && !IsSending;

    /// <summary>
    /// Called after this view-model becomes the active view-model (D-7): re-checks AI availability
    /// every time the screen is entered, including a return trip from Settings after the user
    /// configured AI.
    /// </summary>
    public void OnNavigatedTo() => _ = RefreshAvailabilityAsync();

    /// <summary>
    /// Called before another view-model replaces this one (D-7, D2, S-12): cancels any in-flight
    /// question and clears the conversation, since it lives in memory only.
    /// </summary>
    public void OnNavigatedFrom() => ResetConversation();

    /// <summary>
    /// Sends <see cref="QuestionText"/> to <see cref="AskFinanceChatUseCase"/> and applies the
    /// outcome to the transcript (R-9, D-9). At most one question is in flight at a time. The
    /// <see cref="CanSend"/> predicate disables <c>SendCommand</c> while <see cref="IsSending"/> for
    /// the normal data-bound-button path, and the explicit <see cref="IsSending"/> guard below makes
    /// the no-op hold even for a direct/programmatic invocation that bypasses <c>CanExecute</c> — per
    /// D-11's "one question at a time, with a Stop command" rule (Stop cancels, it does not queue).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (IsSending)
        {
            return;
        }

        string question = QuestionText.Trim();
        QuestionText = string.Empty;

        Messages.Add(ChatMessageItem.User(question));
        var pending = ChatMessageItem.Pending(_localizer["ChatThinking"]);
        Messages.Add(pending);

        int generation = ++_generation;
        var cts = new CancellationTokenSource();
        _sendCts = cts;
        IsSending = true;

        try
        {
            Result result = await _ask.ExecuteAsync(
                new AskFinanceChatCommand(question, _answeredTurns.ToArray()), cts.Token).ConfigureAwait(false);

            if (generation != _generation)
            {
                // A Stop/reset/navigate-away superseded this invocation while it was in flight —
                // its outcome must never render (S-6, S-12).
                return;
            }

            ChatReply reply = result.IsSuccess && result.Value is ChatReply typed
                ? typed
                : new ChatReply(ChatReplyStatus.Unavailable, null);

            switch (reply.Status)
            {
                case ChatReplyStatus.Answered:
                    pending.Complete(reply.Text!, ChatMessageState.Answered);
                    _answeredTurns.Add(new ChatTurn(question, reply.Text!));
                    break;
                case ChatReplyStatus.NotConfigured:
                    pending.Complete(_localizer["ChatAiNotConfiguredBanner"], ChatMessageState.NotConfigured);
                    IsAiConfigured = false;
                    break;
                case ChatReplyStatus.Rejected:
                    pending.Complete(_localizer["ChatQuestionRejected"], ChatMessageState.Rejected);
                    break;
                default:
                    pending.Complete(_localizer["ChatAiUnavailable"], ChatMessageState.Unavailable);
                    break;
            }

            if (reply.Snapshot is not null)
            {
                ApplyFigures(reply.Snapshot, reply.Locale);
            }
        }
        catch (OperationCanceledException)
        {
            // Stop or navigate-away cancelled the per-invocation CTS (R-9, S-6): a localized note,
            // never an error/failure message, and this turn is excluded from _answeredTurns so it
            // can never be replayed.
            if (generation == _generation)
            {
                pending.Complete(_localizer["ChatCancelledNote"], ChatMessageState.Cancelled);
            }
        }
        catch (Exception ex)
        {
            // Defense in depth: AskFinanceChatUseCase.ExecuteAsync is documented to never throw for
            // an AI-side reason (D-5), but no exception may ever reach the UI (R-6).
            _logger.LogWarning(ex, "Chat send failed unexpectedly.");
            if (generation == _generation)
            {
                pending.Complete(_localizer["ChatAiUnavailable"], ChatMessageState.Unavailable);
            }
        }
        finally
        {
            if (generation == _generation)
            {
                IsSending = false;
                _sendCts = null;
            }

            cts.Dispose();
        }
    }

    private bool CanSend() =>
        IsAiConfigured && !IsSending && QuestionText.Trim().Length is >= 1 and <= AskFinanceChatUseCase.MaxQuestionChars;

    /// <summary>Cancels the in-flight <see cref="SendAsync"/> call via its own CTS (R-9).</summary>
    [RelayCommand(CanExecute = nameof(IsSending))]
    private void Stop()
    {
        try
        {
            _sendCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The in-flight invocation already completed and disposed its own CTS between the
            // null-check above and this call — nothing left to cancel.
        }
    }

    /// <summary>Starts a fresh conversation, cancelling any in-flight question (D2).</summary>
    [RelayCommand]
    private void NewConversation() => ResetConversation();

    /// <summary>Copies a bubble's exact displayed text to the clipboard (R-13, S-11).</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyAsync(ChatMessageItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _clipboard.SetTextAsync(item.Text).ConfigureAwait(false);
    }

    private static bool CanCopy(ChatMessageItem? item) => item is not null && item.State != ChatMessageState.Pending;

    /// <summary>
    /// Clears the in-memory conversation (D2): bumps the generation so any in-flight
    /// <see cref="SendAsync"/> completion is recognized as superseded, cancels it, and resets every
    /// observable collection/property the transcript and figures panel expose.
    /// </summary>
    private void ResetConversation()
    {
        ++_generation;

        try
        {
            _sendCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _sendCts = null;
        IsSending = false;

        Messages.Clear();
        _answeredTurns.Clear();

        FigureLines.Clear();
        FiguresMonthLabel = string.Empty;
        FiguresExpenses = string.Empty;
        FiguresIncomes = string.Empty;
        FiguresNet = string.Empty;
        FiguresHaveMissingRates = false;
    }

    private async Task RefreshAvailabilityAsync()
    {
        try
        {
            ChatAvailability availability = await _ask.GetAvailabilityAsync().ConfigureAwait(false);
            IsAiConfigured = availability.IsConfigured;
            PrivacyDisclosure = string.Format(
                CultureInfo.InvariantCulture, _localizer["ChatPrivacyDisclosureFormat"], availability.ProviderName);
        }
        catch (Exception ex)
        {
            // Defense in depth: GetAvailabilityAsync makes no HTTP call and is documented to never
            // throw for an AI-side reason (D-5), but the screen must never crash on load.
            _logger.LogWarning(ex, "Chat availability check failed unexpectedly.");
            IsAiConfigured = false;
        }
        finally
        {
            IsAvailabilityKnown = true;
        }
    }

    /// <summary>
    /// Renders the exact-figure backup panel from <paramref name="snapshot"/> — never from LLM text
    /// (R-10) — using the same <see cref="ChatContextRenderer.FormatDop"/> formatter and culture the
    /// prompt's context used, so the panel and the prompt can never disagree (D-9).
    /// </summary>
    private void ApplyFigures(ChatContextSnapshot snapshot, string? locale)
    {
        string effectiveLocale = SupportedLocales.IsSupported(locale) ? locale! : SupportedLocales.Default;
        var culture = CultureInfo.GetCultureInfo(effectiveLocale);

        FigureLines.Clear();

        if (snapshot.CurrentMonth is not null)
        {
            FiguresMonthLabel = snapshot.CurrentMonth.Month.ToString("MMMM yyyy", culture);
            FiguresExpenses = ChatContextRenderer.FormatDop(snapshot.CurrentMonth.ExpensesDop, culture);
            FiguresIncomes = ChatContextRenderer.FormatDop(snapshot.CurrentMonth.IncomesDop, culture);
            FiguresNet = ChatContextRenderer.FormatDop(snapshot.CurrentMonth.NetBalanceDop, culture);
        }

        if (snapshot.CurrentCategories is not null)
        {
            foreach (ChatCategoryLine line in snapshot.CurrentCategories.Lines)
            {
                FigureLines.Add(new ChatFigureLine(line.Name, ChatContextRenderer.FormatDop(line.AmountDop, culture)));
            }

            if (snapshot.CurrentCategories.OverflowCount > 0)
            {
                // Reuses the same prompt-data label the context renderer uses for this exact line
                // (Application-layer ChatContextLabels, not UI copy — R-15 scopes to UI strings),
                // so the panel and the prompt can never disagree on wording either (D-9).
                ChatContextLabelSet labels = ChatContextLabels.For(effectiveLocale);
                string overflowLine = string.Format(
                    culture,
                    labels.OverflowLineFormat,
                    snapshot.CurrentCategories.OverflowCount,
                    ChatContextRenderer.FormatDop(snapshot.CurrentCategories.OverflowAmountDop, culture));
                FigureLines.Add(new ChatFigureLine(overflowLine, string.Empty));
            }
        }

        FiguresHaveMissingRates = snapshot.HasMissingRates;
    }
}

/// <summary>
/// One line of the exact-figure backup panel (R-10, D-9): a category name (or the overflow label)
/// paired with its already-formatted DOP amount, sharing the panel's culture with the prompt context.
/// </summary>
public sealed record ChatFigureLine(string Name, string AmountDisplay);
