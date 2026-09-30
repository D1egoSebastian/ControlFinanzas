using Avalonia.Controls;
using Avalonia.Threading;
using ControlDeFinanzasIA.UI.Behaviors;
using ControlDeFinanzasIA.UI.Input;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>F-021 S4 (R-12, R-14, DD-5): the REAL <see cref="FocusOnRequest"/> behavior on real controls.</summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class FocusOnRequestTests
{
    private static void Settle()
    {
        HeadlessUi.Flush();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
        HeadlessUi.Flush();
    }

    [Fact]
    public async Task ANewToken_FocusesTheControl()
    {
        (bool Before, bool After) result = await HeadlessUi.OnUiThread(() =>
        {
            var box = new TextBox();
            var other = new TextBox();
            Window window = HeadlessUi.Show(new StackPanel { Children = { other, box } }, new object());
            using var scope = new HeadlessUi.Scope(window);
            other.Focus();
            Settle();
            bool before = box.IsFocused;

            FocusOnRequest.SetToken(box, new FocusRequestToken());
            Settle();

            return Task.FromResult((before, box.IsFocused));
        });

        Assert.False(result.Before);
        Assert.True(result.After);
    }

    [Fact]
    public async Task TheSamePropertySetTwiceWithFreshTokens_FocusesTwice()
    {
        (bool First, bool Between, bool Second) result = await HeadlessUi.OnUiThread(() =>
        {
            var box = new TextBox();
            var other = new TextBox();
            Window window = HeadlessUi.Show(new StackPanel { Children = { other, box } }, new object());
            using var scope = new HeadlessUi.Scope(window);

            FocusOnRequest.SetToken(box, new FocusRequestToken());
            Settle();
            bool first = box.IsFocused;

            other.Focus();
            Settle();
            bool between = box.IsFocused;

            FocusOnRequest.SetToken(box, new FocusRequestToken());
            Settle();
            return Task.FromResult((first, between, box.IsFocused));
        });

        Assert.True(result.First);
        Assert.False(result.Between);
        Assert.True(result.Second);
    }

    [Fact]
    public async Task ATokenRaisedBeforeTheControlIsAttached_FocusesOnceAttached()
    {
        (bool BeforeAttach, bool AfterAttach) result = await HeadlessUi.OnUiThread(() =>
        {
            var box = new TextBox();
            FocusOnRequest.SetToken(box, new FocusRequestToken());
            Settle();
            bool before = box.IsFocused;

            var host = new StackPanel();
            Window window = HeadlessUi.Show(host, new object());
            using var scope = new HeadlessUi.Scope(window);
            host.Children.Add(box);
            Settle();
            return Task.FromResult((before, box.IsFocused));
        });

        Assert.False(result.BeforeAttach);
        Assert.True(result.AfterAttach);
    }

    [Fact]
    public async Task ADisabledControl_IsNotFocused_AndDoesNotThrow()
    {
        (bool DisabledFocused, bool EnabledFocused) result = await HeadlessUi.OnUiThread(() =>
        {
            var disabled = new TextBox { IsEnabled = false };
            var enabled = new TextBox();
            Window window = HeadlessUi.Show(new StackPanel { Children = { disabled, enabled } }, new object());
            using var scope = new HeadlessUi.Scope(window);

            FocusOnRequest.SetToken(disabled, new FocusRequestToken());
            Settle();
            bool disabledFocused = disabled.IsFocused;

            FocusOnRequest.SetToken(enabled, new FocusRequestToken());
            Settle();
            return Task.FromResult((disabledFocused, enabled.IsFocused));
        });

        Assert.False(result.DisabledFocused);
        Assert.True(result.EnabledFocused);
    }

    [Fact]
    public async Task FirstFocusableToken_FocusesTheFirstTabStopInsideTheHost_SkippingDisabledOnes()
    {
        (bool Skipped, bool First, bool Third) result = await HeadlessUi.OnUiThread(() =>
        {
            var skipped = new TextBox { IsEnabled = false };
            var first = new TextBox();
            var third = new TextBox();
            var host = new ContentControl { Content = new StackPanel { Children = { skipped, first, third } } };
            Window window = HeadlessUi.Show(host, new object());
            using var scope = new HeadlessUi.Scope(window);

            FocusOnRequest.SetFirstFocusableToken(host, new FocusRequestToken());
            Settle();
            return Task.FromResult((skipped.IsFocused, first.IsFocused, third.IsFocused));
        });

        Assert.False(result.Skipped);
        Assert.True(result.First);
        Assert.False(result.Third);
    }
}
