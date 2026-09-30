using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.Services;
using ErrorIds = ControlDeFinanzasIA.UI.Diagnostics.CorrelationId;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// State of the in-window error overlay (F-021, DD-4). Singleton.
/// </summary>
public sealed partial class ErrorOverlayViewModel : ViewModelBase, IErrorPresenter
{
    private readonly IClipboardService _clipboard;
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

    [ObservableProperty]
    private FocusRequestToken? _initialFocus;

    [ObservableProperty]
    private bool _isCopied;

    /// <summary>Initializes a new instance of the <see cref="ErrorOverlayViewModel"/> class.</summary>
    /// <param name="clipboard">Clipboard used by the copy command.</param>
    /// <param name="appVersion">Application version included in the copy payload.</param>
    public ErrorOverlayViewModel(IClipboardService clipboard, string appVersion)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        _clipboard = clipboard;
        _appVersion = appVersion;
    }

    /// <summary>Gets the short id (8 lowercase hex characters), empty while closed.</summary>
    public string ShortId => CorrelationId == Guid.Empty ? string.Empty : ErrorIds.Short(CorrelationId);

    /// <summary>Gets the "+N more" caption, or <see langword="null"/> when no further error arrived.</summary>
    public string? AdditionalText => AdditionalCount > 0
        ? string.Format(CultureInfo.CurrentUICulture, Strings.ErrorOverlayMore, AdditionalCount)
        : null;

    /// <summary>
    /// Gets the invariant diagnostic record for the clipboard: version, id and UTC time only. It carries no
    /// message, exception type, stack or path.
    /// </summary>
    public string CopyText => CorrelationId == Guid.Empty
        ? string.Empty
        : $"ControlDeFinanzasIA {_appVersion}\nError ID: {ShortId} ({CorrelationId:D})\n"
            + $"Time (UTC): {OccurredAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Copies <see cref="CopyText"/> to the clipboard. Best effort: a clipboard failure leaves
    /// <see cref="IsCopied"/> false and never throws, because this command runs while the app is already
    /// reporting a failure.
    /// </summary>
    /// <returns>A task that completes when the copy attempt ends.</returns>
    [RelayCommand]
    public async Task CopyAsync()
    {
        try
        {
            await _clipboard.SetTextAsync(CopyText);
            IsCopied = true;
        }
        catch (Exception)
        {
            IsCopied = false;
        }
    }

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
        IsCopied = false;
        IsOpen = true;
        InitialFocus = new FocusRequestToken();
    }

    /// <summary>Closes the overlay and resets every field.</summary>
    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        CorrelationId = Guid.Empty;
        OccurredAtUtc = default;
        AdditionalCount = 0;
        IsCopied = false;
        InitialFocus = null;
    }
}
