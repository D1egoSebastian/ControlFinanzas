using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;

namespace ControlDeFinanzasIA.UI.Converters;

/// <summary>
/// Converts the active navigation view-model into its concrete view.
/// </summary>
public sealed class CurrentViewConverter : IValueConverter
{
    /// <summary>Shared stateless converter instance for XAML bindings.</summary>
    public static CurrentViewConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
        => value switch
        {
            DashboardViewModel viewModel => new DashboardView
            {
                DataContext = viewModel,
            },
            ExpensesViewModel viewModel => new ExpensesView
            {
                DataContext = viewModel,
            },
            IncomeViewModel viewModel => new IncomeView
            {
                DataContext = viewModel,
            },
            RecurringRulesViewModel viewModel => new RecurringRulesView
            {
                DataContext = viewModel,
            },
            _ => null,
        };

    /// <inheritdoc />
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
        => throw new NotSupportedException();
}
