using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Who a <see cref="ChatMessageItem"/> represents in the conversation transcript (F-015, D-9).
/// </summary>
public enum ChatMessageRole
{
    /// <summary>The user's own question.</summary>
    User,

    /// <summary>The assistant's reply, or a system-generated note standing in for one.</summary>
    Assistant,
}

/// <summary>
/// Lifecycle state of an assistant bubble (F-015, D-9, D-10). <see cref="Pending"/> is the only
/// state a <see cref="ChatMessageItem"/> starts in for an assistant message before
/// <see cref="ChatMessageItem.Complete"/> resolves it; a user message is created already
/// <see cref="Answered"/> and never transitions.
/// </summary>
public enum ChatMessageState
{
    /// <summary>Waiting on <c>AskFinanceChatUseCase.ExecuteAsync</c> to complete.</summary>
    Pending,

    /// <summary>The model answered and <see cref="ChatMessageItem.Text"/> holds its reply.</summary>
    Answered,

    /// <summary>The AI pipeline failed (exception, timeout, empty response) — R-6.</summary>
    Unavailable,

    /// <summary>The user issued Stop and the in-flight call was cancelled — R-9, S-6.</summary>
    Cancelled,

    /// <summary>The trimmed question was outside the 1-500 char range — R-4.</summary>
    Rejected,

    /// <summary>AI was not configured when the question was sent — R-12.</summary>
    NotConfigured,
}

/// <summary>
/// A single bubble in the chat transcript (F-015, D-9): either the user's own question, or an
/// assistant bubble that starts <see cref="ChatMessageState.Pending"/> and is later resolved by
/// <see cref="Complete"/> to one terminal <see cref="ChatMessageState"/>. <see cref="IsNote"/>
/// tells the view to render a bubble with muted styling instead of a normal reply (R-9, R-12).
/// </summary>
public sealed partial class ChatMessageItem : ObservableObject
{
    /// <summary>Gets who this bubble represents. Never changes after construction.</summary>
    public ChatMessageRole Role { get; }

    /// <summary>Gets the bubble's displayed text — the exact text a Copy command must reproduce (R-13).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNote))]
    private string _text;

    /// <summary>Gets the bubble's current lifecycle state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNote))]
    private ChatMessageState _state;

    /// <summary>
    /// Gets a value indicating whether this bubble is a system-generated note rather than a real
    /// assistant reply — <see cref="ChatMessageState.Cancelled"/>, <see cref="ChatMessageState.Rejected"/>,
    /// <see cref="ChatMessageState.NotConfigured"/>, or <see cref="ChatMessageState.Unavailable"/> — so
    /// the view can render it with muted styling (D-9, D-10).
    /// </summary>
    public bool IsNote => State is ChatMessageState.Cancelled
        or ChatMessageState.Rejected
        or ChatMessageState.NotConfigured
        or ChatMessageState.Unavailable;

    /// <summary>Gets a value indicating whether this bubble is the user's own (the view tints it with the accent wash).</summary>
    public bool IsUser => Role == ChatMessageRole.User;

    private ChatMessageItem(ChatMessageRole role, string text, ChatMessageState state)
    {
        Role = role;
        _text = text;
        _state = state;
    }

    /// <summary>Creates the user's own bubble for <paramref name="text"/>, already <see cref="ChatMessageState.Answered"/>.</summary>
    public static ChatMessageItem User(string text) => new(ChatMessageRole.User, text, ChatMessageState.Answered);

    /// <summary>
    /// Creates the assistant placeholder bubble shown while a question is in flight, displaying
    /// <paramref name="placeholderText"/> until <see cref="Complete"/> resolves it.
    /// </summary>
    public static ChatMessageItem Pending(string placeholderText) =>
        new(ChatMessageRole.Assistant, placeholderText, ChatMessageState.Pending);

    /// <summary>
    /// Resolves a <see cref="ChatMessageState.Pending"/> assistant bubble to its terminal text and
    /// state (Answered, Unavailable, Cancelled, Rejected, or NotConfigured).
    /// </summary>
    public void Complete(string text, ChatMessageState state)
    {
        Text = text;
        State = state;
    }
}
