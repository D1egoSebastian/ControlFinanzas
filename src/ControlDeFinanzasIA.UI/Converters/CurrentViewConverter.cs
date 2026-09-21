using System.Globalization;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.UI.Views;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Converts the active navigation view-model into its concrete view.
/// </summary>
/// <remarks>
/// Delegates to <see cref="ViewLocator"/> instead of keeping its own
/// view-model-to-view map: a second map here silently fell out of sync
/// with <see cref="ViewLocator"/> for five phases (Debts through
/// Settings), rendering those routes' content area blank because this
/// converter — not <see cref="ViewLocator"/> — is what
/// <c>MainWindow.axaml</c>'s <c>ContentControl</c> actually binds
/// through. One map, kept current every phase, replaces two.
/// </remarks>
public sealed class CurrentViewConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static CurrentViewConverter Instance { get; } = new();

    private readonly ViewLocator _viewLocator = new();

    /// <inheritdoc />
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
        => _viewLocator.Build(value);

    /// <inheritdoc />
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
        => throw new NotSupportedException();
}
