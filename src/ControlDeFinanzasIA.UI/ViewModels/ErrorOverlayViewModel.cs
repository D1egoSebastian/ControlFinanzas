using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.UI.Diagnostics;
using ErrorIds = ControlDeFinanzasIA.UI.Diagnostics.CorrelationId;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// State of the in-window error overlay (F-021, DD-4). Singleton. This slice holds the state only; the clipboard
/// copy command, the localized captions and the view arrive with the overlay view.
/// </summary>
public sealed partial class ErrorOverlayViewModel : ViewModelBase, IErrorPresenter
{
    private readonly string _appVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortId))]
    [NotifyPropertyChangedFor(nameof(CopyText))]
    private Guid _correlationId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyText))]
    private DateTime _occurredAtUtc;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AdditionalText))]
    private int _additionalCount;

    [ObservableProperty]
    private bool _isOpen;

    /// <summary>Initializes a new instance of the <see cref="ErrorOverlayViewModel"/> class.</summary>
    /// <param name="appVersion">Application version included in the copy payload.</param>
    public ErrorOverlayViewModel(string appVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        _appVersion = appVersion;
    }

    /// <summary>Gets the short id (8 lowercase hex characters), empty while closed.</summary>
    public string ShortId => CorrelationId == Guid.Empty ? string.Empty : ErrorIds.Short(CorrelationId);

    /// <summary>Gets the "+N more" caption, or <see langword="null"/> when no further error arrived.</summary>
    public string? AdditionalText => AdditionalCount > 0 ? $"+{AdditionalCount} more" : null;

    /// <summary>
    /// Gets the invariant diagnostic record for the clipboard: version, id and UTC time only. It carries no
    /// message, exception type, stack or path.
    /// </summary>
    public string CopyText => CorrelationId == Guid.Empty
        ? string.Empty
        : $"ControlDeFinanzasIA {_appVersion}\nError ID: {ShortId} ({CorrelationId:D})\n"
            + $"Time (UTC): {OccurredAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}";

    /// <summary>Opens the overlay for the first error; later ones while open only increase <see cref="AdditionalCount"/>.</summary>
    /// <param name="correlationId">The id written to the log.</param>
    /// <param name="occurredAtUtc">UTC time of the failure.</param>
    public void Present(Guid correlationId, DateTime occurredAtUtc)
    {
        if (IsOpen)
        {
            AdditionalCount++;
            return;
        }

        CorrelationId = correlationId;
        OccurredAtUtc = occurredAtUtc;
        AdditionalCount = 0;
        IsOpen = true;
    }

    /// <summary>Closes the overlay and resets every field.</summary>
    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        CorrelationId = Guid.Empty;
        OccurredAtUtc = default;
        AdditionalCount = 0;
    }
}
