using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.UI.Controls;

/// <summary>
/// The stock <see cref="DatePicker"/> with its empty-state words ("day", "month", "year") taken from the app's resx
/// instead of the hard-coded English the control paints while no date is picked. It looks up the stock template and
/// styles (<see cref="StyleKeyOverride"/>), so nothing else about the picker changes. Presentation only.
/// </summary>
public sealed class LocalizedDatePicker : DatePicker
{
    private TextBlock? _day;
    private TextBlock? _month;
    private TextBlock? _year;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(DatePicker);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _day = e.NameScope.Find<TextBlock>("PART_DayTextBlock");
        _month = e.NameScope.Find<TextBlock>("PART_MonthTextBlock");
        _year = e.NameScope.Find<TextBlock>("PART_YearTextBlock");
        ApplyPlaceholders();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDateProperty)
        {
            ApplyPlaceholders();
        }
    }

    private void ApplyPlaceholders()
    {
        if (SelectedDate is not null)
        {
            return;
        }

        if (_day is not null)
        {
            _day.Text = Strings.DatePickerDayPlaceholder;
        }

        if (_month is not null)
        {
            _month.Text = Strings.DatePickerMonthPlaceholder;
        }

        if (_year is not null)
        {
            _year.Text = Strings.DatePickerYearPlaceholder;
        }
    }
}
