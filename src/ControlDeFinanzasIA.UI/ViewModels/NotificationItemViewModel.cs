namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Immutable display row for one notification in the flyout (F-017, D-10).
/// </summary>
/// <param name="Id">Identifier of the persisted notification.</param>
/// <param name="IsUnread">Whether the notification has not been read yet.</param>
/// <param name="Title">Pre-formatted, localized title.</param>
/// <param name="Body">Pre-formatted, localized body.</param>
public sealed record NotificationItemViewModel(Guid Id, bool IsUnread, string Title, string Body);
