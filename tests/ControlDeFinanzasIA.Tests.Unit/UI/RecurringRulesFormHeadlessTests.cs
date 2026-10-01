using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Pre-1.0 slice C: the REAL <see cref="RecurringRulesView"/> over the REAL view model and use cases. The form asks
/// for the day of the month the charge is made, shows the derived next charge read-only, and editing through the
/// real Save button never moves the stored schedule.
/// </summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class RecurringRulesFormHeadlessTests
{
    private static readonly DateOnly _sep20 = new(2026, 9, 20);

    private static ComboBox ComboNamed(Control view, string name) =>
        view.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == name);

    private static string[] VisibleTexts(Control view) =>
        [.. view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];

    private static Button ButtonFor(Control view, System.Windows.Input.ICommand command) =>
        view.GetVisualDescendants().OfType<Button>().First(b => ReferenceEquals(b.Command, command));

    [Fact]
    public async Task TheForm_HasNoRawDateField_AndAsksForTheDayOfTheMonth()
    {
        bool ok = await HeadlessUi.OnUiThread(async () =>
        {
            RecurringRulesViewModelScheduleTests.Rig rig = await RecurringRulesViewModelScheduleTests.NewAsync(_sep20);
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.Vm));

            Assert.Empty(view.GetVisualDescendants().OfType<DatePicker>());
            ComboBox day = ComboNamed(view, Strings.RecurringDayOfMonthLabel);
            Assert.Equal(31, day.ItemCount);
            Assert.Equal(20, day.SelectedItem);
            Assert.Contains(Strings.RecurringDayOfMonthLabel, VisibleTexts(view));
            Assert.Contains(Strings.RecurringDayOfMonthHelp, VisibleTexts(view));
            Assert.Contains(Strings.RecurringTypeHelpSubscription, VisibleTexts(view));
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task TheDayCombo_IsTwoWayWithTheViewModel_AndTheNextChargeFollowsIt()
    {
        bool ok = await HeadlessUi.OnUiThread(async () =>
        {
            RecurringRulesViewModelScheduleTests.Rig rig = await RecurringRulesViewModelScheduleTests.NewAsync(_sep20);
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.Vm));
            ComboBox day = ComboNamed(view, Strings.RecurringDayOfMonthLabel);

            rig.Vm.DayOfMonth = 11;
            HeadlessUi.Flush();
            Assert.Equal(11, day.SelectedItem);

            day.SelectedItem = 27;
            HeadlessUi.Flush();
            Assert.Equal(27, rig.Vm.DayOfMonth);
            string expected = string.Format(CultureInfo.CurrentUICulture, Strings.RecurringNextChargeFormat, new DateOnly(2026, 9, 27));
            Assert.Contains(expected, VisibleTexts(view));
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task TheMonthCombo_AppearsOnlyForAYearlyCharge_AndTheTypeHelpFollowsTheType()
    {
        bool ok = await HeadlessUi.OnUiThread(async () =>
        {
            RecurringRulesViewModelScheduleTests.Rig rig = await RecurringRulesViewModelScheduleTests.NewAsync(_sep20);
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.Vm));
            ComboBox month = ComboNamed(view, Strings.RecurringMonthOfChargeLabel);
            Assert.False(month.IsEffectivelyVisible);

            rig.Vm.Frequency = "Yearly";
            rig.Vm.Month = 3;
            HeadlessUi.Flush();
            Assert.True(month.IsEffectivelyVisible);
            Assert.Equal(12, month.ItemCount);
            Assert.Equal(3, month.SelectedValue);
            Assert.Contains(Strings.RecurringYearlyDateLabel, VisibleTexts(view));

            rig.Vm.RuleType = "FixedExpense";
            HeadlessUi.Flush();
            Assert.Contains(Strings.RecurringTypeHelpFixedExpense, VisibleTexts(view));
            Assert.DoesNotContain(Strings.RecurringTypeHelpSubscription, VisibleTexts(view));
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task EditingARuleThroughTheRealSaveButton_KeepsTheStoredSchedule()
    {
        // Last month's date whose occurrence in the current month (the 25th) has not arrived yet.
        var start = new DateOnly(2026, 8, 25);
        bool ok = await HeadlessUi.OnUiThread(async () =>
        {
            Subscription rule = RecurringRule.Create(
                "Internet", ControlDeFinanzasIA.Domain.ValueObjects.Money.Create(30m, ControlDeFinanzasIA.Domain.Enums.Currency.DOP),
                ControlDeFinanzasIA.Domain.Enums.Currency.DOP, "Monthly", start, null,
                RecurringRulesViewModelScheduleTests.CategoryId, RecurringRulesViewModelScheduleTests.MethodId);
            RecurringRulesViewModelScheduleTests.Rig rig = await RecurringRulesViewModelScheduleTests.NewAsync(_sep20, rule);
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.Vm));

            Button edit = view.GetVisualDescendants().OfType<Button>().First(b => ReferenceEquals(b.CommandParameter, rule) && ReferenceEquals(b.Command, rig.Vm.EditCommand));
            edit.Command!.Execute(edit.CommandParameter);
            HeadlessUi.Flush();
            Assert.Equal(25, ComboNamed(view, Strings.RecurringDayOfMonthLabel).SelectedItem);

            rig.Vm.Name = "Internet Pro";
            HeadlessUi.Flush();
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)ButtonFor(view, rig.Vm.SaveCommand).Command!).ExecuteAsync(null);
            HeadlessUi.Flush();

            Assert.Equal("Internet Pro", rule.Name);
            Assert.Equal(start, rule.StartDate);
            Assert.Equal(start, rule.NextGenerationDate);
            Assert.Equal(string.Empty, rig.Vm.FormError);
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task EachRowShowsItsNextCharge_AndItsSkipButtonCarriesItsOwnRule()
    {
        bool ok = await HeadlessUi.OnUiThread(async () =>
        {
            Subscription rule = RecurringRule.Create(
                "Internet", ControlDeFinanzasIA.Domain.ValueObjects.Money.Create(30m, ControlDeFinanzasIA.Domain.Enums.Currency.DOP),
                ControlDeFinanzasIA.Domain.Enums.Currency.DOP, "Monthly", new DateOnly(2026, 9, 25), null,
                RecurringRulesViewModelScheduleTests.CategoryId, RecurringRulesViewModelScheduleTests.MethodId);
            RecurringRulesViewModelScheduleTests.Rig rig = await RecurringRulesViewModelScheduleTests.NewAsync(_sep20, rule);
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.Vm));

            Button skip = view.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, rig.Vm.SkipCommand));
            Assert.Same(rule, skip.CommandParameter);
            Assert.Equal(Strings.RecurringSkipButton, skip.Content);
            Assert.Contains(
                VisibleTexts(view),
                t => t.StartsWith(Strings.RecurringNextChargeFormat[..Strings.RecurringNextChargeFormat.IndexOf('{', StringComparison.Ordinal)], StringComparison.Ordinal)
                    && t.Contains("25", StringComparison.Ordinal));
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }

    [Fact]
    public async Task DeactivateAndDelete_AreOnlyOfferedWhileARuleIsBeingEdited()
    {
        bool ok = await HeadlessUi.OnUiThread(async () =>
        {
            Subscription rule = RecurringRule.Create(
                "Internet", ControlDeFinanzasIA.Domain.ValueObjects.Money.Create(30m, ControlDeFinanzasIA.Domain.Enums.Currency.DOP),
                ControlDeFinanzasIA.Domain.Enums.Currency.DOP, "Monthly", new DateOnly(2026, 9, 25), null,
                RecurringRulesViewModelScheduleTests.CategoryId, RecurringRulesViewModelScheduleTests.MethodId);
            RecurringRulesViewModelScheduleTests.Rig rig = await RecurringRulesViewModelScheduleTests.NewAsync(_sep20, rule);
            var view = new RecurringRulesView();
            using var _ = new HeadlessUi.Scope(HeadlessUi.Show(view, rig.Vm));

            Assert.False(ButtonFor(view, rig.Vm.DeactivateSelectedCommand).IsEffectivelyVisible);
            Assert.False(ButtonFor(view, rig.Vm.DeleteSelectedCommand).IsEffectivelyVisible);

            rig.Vm.EditCommand.Execute(rule);
            HeadlessUi.Flush();

            Assert.True(ButtonFor(view, rig.Vm.DeactivateSelectedCommand).IsEffectivelyVisible);
            Assert.True(ButtonFor(view, rig.Vm.DeleteSelectedCommand).IsEffectivelyVisible);
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ok);
    }
}
