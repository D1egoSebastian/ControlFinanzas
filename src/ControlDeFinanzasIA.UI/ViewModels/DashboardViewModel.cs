using CommunityToolkit.Mvvm.ComponentModel;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Localization;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Placeholder view-model for the dashboard content area.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer for UI strings.</param>
    public DashboardViewModel(IStringLocalizer<Strings> localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        _title = localizer["DashboardPlaceholder"];
    }

    /// <summary>
    /// Gets or sets the localized title displayed in the dashboard.
    /// </summary>
    [ObservableProperty]
    private string _title;
}
