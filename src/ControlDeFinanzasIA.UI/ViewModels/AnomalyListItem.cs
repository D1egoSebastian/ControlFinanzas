using CommunityToolkit.Mvvm.ComponentModel;
using ControlDeFinanzasIA.Domain.Alerts;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Observable row for the Dashboard's unusual-expenses (anomalies) list
/// (F-014, D-12). Identity and display facts are immutable, set from the
/// deterministic <see cref="Application.UseCases.Dashboard.DetectAnomalies.AnomalyItemDto"/>
/// on load; <see cref="Message"/> and <see cref="IsNarrated"/> are
/// observable so the non-blocking narration pass (S8b) can replace the
/// deterministic template text in place, without rebuilding the list.
/// </summary>
public sealed partial class AnomalyListItem : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnomalyListItem"/> class.
    /// </summary>
    /// <param name="alertId">Identifier of the persisted <c>AnomalyAlert</c>.</param>
    /// <param name="categoryName">Display name of the flagged expense's category.</param>
    /// <param name="description">Short description of the flagged expense.</param>
    /// <param name="amountDisplay">Pre-formatted, DOP-normalized amount.</param>
    /// <param name="dateDisplay">Pre-formatted expense date.</param>
    /// <param name="severity">Anomaly severity.</param>
    /// <param name="severityLabel">Localized display label for <paramref name="severity"/>.</param>
    /// <param name="message">Initial message text, the deterministic template (D-7).</param>
    public AnomalyListItem(
        Guid alertId,
        string categoryName,
        string description,
        string amountDisplay,
        string dateDisplay,
        AnomalySeverity severity,
        string severityLabel,
        string message)
    {
        AlertId = alertId;
        CategoryName = categoryName;
        Description = description;
        AmountDisplay = amountDisplay;
        DateDisplay = dateDisplay;
        Severity = severity;
        SeverityLabel = severityLabel;
        _message = message;
    }

    /// <summary>Gets the identifier of the persisted <c>AnomalyAlert</c>.</summary>
    public Guid AlertId { get; }

    /// <summary>Gets the display name of the flagged expense's category.</summary>
    public string CategoryName { get; }

    /// <summary>Gets the short description of the flagged expense.</summary>
    public string Description { get; }

    /// <summary>Gets the pre-formatted, DOP-normalized amount.</summary>
    public string AmountDisplay { get; }

    /// <summary>Gets the pre-formatted expense date.</summary>
    public string DateDisplay { get; }

    /// <summary>Gets the anomaly severity.</summary>
    public AnomalySeverity Severity { get; }

    /// <summary>Gets the localized display label for <see cref="Severity"/>.</summary>
    public string SeverityLabel { get; }

    /// <summary>
    /// Gets or sets the message shown for this row: the deterministic
    /// template (D-7) until the narration pass (S8b) replaces it with an
    /// LLM-generated sentence. Never cleared by a failed narration pass.
    /// </summary>
    [ObservableProperty]
    private string _message;

    /// <summary>Gets or sets a value indicating whether <see cref="Message"/> was replaced by the narration pass.</summary>
    [ObservableProperty]
    private bool _isNarrated;
}
