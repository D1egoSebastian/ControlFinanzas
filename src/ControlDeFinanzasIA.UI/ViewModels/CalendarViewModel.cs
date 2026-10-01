using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Calendar.GetCalendarMonth;
using ControlDeFinanzasIA.UI.Formatting;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.Resources;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// View-model for the expense calendar screen (F-009, slice 3): monthly
/// grid navigation (R-1), DOP-normalized daily badges (R-2), adjacent-month
/// filler days that navigate the grid instead of drilling down (R-3), and
/// day-click drill-down into Gastos filtered to that exact day (R-4).
/// </summary>
public sealed partial class CalendarViewModel : ViewModelBase
{
    private readonly GetCalendarMonthQuery _getCalendarMonthQuery;
    private readonly INavigationService _navigationService;
    private readonly IClock _clock;
    private readonly ILogger<CalendarViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarViewModel"/>
    /// class. Starts on the current month (<see cref="IClock.Today"/>) and
    /// kicks off the initial load, following the same constructor-load
    /// pattern as <see cref="DashboardViewModel"/>.
    /// </summary>
    public CalendarViewModel(
        GetCalendarMonthQuery getCalendarMonthQuery,
        INavigationService navigationService,
        IClock clock,
        ILogger<CalendarViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(getCalendarMonthQuery);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _getCalendarMonthQuery = getCalendarMonthQuery;
        _navigationService = navigationService;
        _clock = clock;
        _logger = logger;

        _selectedMonth = MonthPeriod.Anchor(_clock.Today);
        _selectedMonthLabel = FormatMonthLabel(_selectedMonth);
        WeekdayHeaders = BuildWeekdayHeaders(UiCulture.Current);

        _ = LoadAsync();
    }

    /// <summary>Gets or sets the currently navigated month (always a month anchor, i.e. day 1).</summary>
    [ObservableProperty]
    private DateOnly _selectedMonth;

    /// <summary>Gets or sets the localized "MMMM yyyy" label of <see cref="SelectedMonth"/> (R-7).</summary>
    [ObservableProperty]
    private string _selectedMonthLabel;

    /// <summary>Gets or sets the pre-formatted, DOP-normalized total for <see cref="SelectedMonth"/>.</summary>
    [ObservableProperty]
    private string _monthTotalDisplay = string.Empty;

    /// <summary>Gets or sets a value indicating whether a load is in progress.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets the error message shown when a load fails.</summary>
    [ObservableProperty]
    private string _loadError = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether at least one daily total
    /// failed to convert to DOP for the currently navigated month.
    /// </summary>
    [ObservableProperty]
    private bool _hasMissingRates;

    /// <summary>
    /// Gets the grid cells for <see cref="SelectedMonth"/>: always exactly
    /// <see cref="CalendarGrid.CellCount"/> (42) after each successful load
    /// (R-1, R-3).
    /// </summary>
    public ObservableCollection<CalendarCell> Cells { get; } = [];

    /// <summary>
    /// Gets the 7 weekday headers, rotated to start at
    /// <see cref="CultureInfo.CurrentUICulture"/>'s
    /// <see cref="DateTimeFormatInfo.FirstDayOfWeek"/> (R-7). Computed once
    /// in the constructor: the week layout does not change across months.
    /// </summary>
    public IReadOnlyList<string> WeekdayHeaders { get; }

    /// <summary>Navigates to the month immediately before <see cref="SelectedMonth"/> and reloads (R-1, S-2).</summary>
    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        SelectedMonth = MonthPeriod.Previous(SelectedMonth);
        await LoadAsync();
    }

    /// <summary>Navigates to the month immediately after <see cref="SelectedMonth"/> and reloads (R-1, S-2).</summary>
    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedMonth = MonthPeriod.Next(SelectedMonth);
        await LoadAsync();
    }

    /// <summary>
    /// Handles a day click. A day of an adjacent month (<see cref="CalendarCell.IsCurrentMonth"/>
    /// <see langword="false"/>) navigates the calendar grid to the month it
    /// belongs to and does NOT drill down (R-3, S-6). A day of the
    /// currently navigated month drills down into Gastos filtered to that
    /// exact day, via the additive typed navigation overload (R-4, S-3/S-4,
    /// design.md "Data Flow").
    /// </summary>
    [RelayCommand]
    private async Task SelectDayAsync(CalendarCell? cell)
    {
        if (cell is null)
        {
            return;
        }

        if (!cell.IsCurrentMonth)
        {
            SelectedMonth = MonthPeriod.Anchor(cell.Date);
            await LoadAsync();
            return;
        }

        _navigationService.NavigateTo<ExpensesViewModel>(
            RouteNames.Expenses,
            vm => vm.ApplyDayFilter(cell.Date));
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadError = string.Empty;
        HasMissingRates = false;
        SelectedMonthLabel = FormatMonthLabel(SelectedMonth);

        try
        {
            Result result = await _getCalendarMonthQuery.ExecuteAsync(
                SelectedMonth,
                UiCulture.Current.DateTimeFormat.FirstDayOfWeek);

            if (!result.IsSuccess)
            {
                LoadError = Strings.CalendarLoadErrorMessage;
                _logger.LogError(
                    "Unable to load the calendar for {Month}: {ErrorCode} {ErrorMessage}",
                    SelectedMonth,
                    result.Error?.Code,
                    result.Error?.Message);
                return;
            }

            var calendar = (CalendarMonthResult)result.Value!;
            HasMissingRates = calendar.HasMissingRates;
            MonthTotalDisplay = MoneyFormat.ApproximateDop(calendar.MonthTotalDop);

            Cells.Clear();
            foreach (CalendarDay day in calendar.Days)
            {
                Cells.Add(new CalendarCell(
                    day.Date,
                    day.Date.Day.ToString(UiCulture.Current),
                    day.TotalDop is decimal total ? MoneyFormat.ApproximateDop(total) : string.Empty,
                    day.IsCurrentMonth,
                    day.IsToday));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load the calendar for {Month}.", SelectedMonth);
            LoadError = Strings.CalendarLoadErrorMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatMonthLabel(DateOnly month)
        => UiCulture.MonthYear(month);

    private static IReadOnlyList<string> BuildWeekdayHeaders(CultureInfo culture)
        => Enumerable.Range(0, 7)
            .Select(i => culture.DateTimeFormat.AbbreviatedDayNames[
                ((int)culture.DateTimeFormat.FirstDayOfWeek + i) % 7])
            .ToArray();
}
