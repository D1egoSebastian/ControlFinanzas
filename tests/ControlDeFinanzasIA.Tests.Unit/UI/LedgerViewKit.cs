using System.Globalization;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Shared helpers of the headless list-view tests (Expenses and Income): culture-pinned UI thread, waiting for
/// asynchronous loads, finding a bound button by its command, and pressing it with the keyboard like a user.
/// </summary>
internal static class LedgerViewKit
{
    internal static Task<T> OnUi<T>(Func<Task<T>> body) =>
        HeadlessUi.OnUiThread(async () =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            try
            {
                return await body();
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        });

    internal static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 300 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        HeadlessUi.Flush();
    }

    internal static Button ButtonFor(Control root, ICommand command, object? parameter = null)
    {
        List<Button> matches =
        [
            .. root.GetVisualDescendants().OfType<Button>()
                .Where(b => ReferenceEquals(b.Command, command) && (parameter is null || ReferenceEquals(b.CommandParameter, parameter))),
        ];

        Assert.NotEmpty(matches);
        return matches[0];
    }

    internal static Button[] VisibleButtonsFor(Control root, ICommand command, object? parameter = null) =>
        [
            .. root.GetVisualDescendants().OfType<Button>()
                .Where(b => ReferenceEquals(b.Command, command) && b.IsEffectivelyVisible && (parameter is null || ReferenceEquals(b.CommandParameter, parameter))),
        ];

    internal static void Press(Window window, Button button)
    {
        Assert.True(button.IsEffectivelyVisible, "The button must be visible to be pressed.");
        Assert.True(button.IsEffectivelyEnabled, "The button must be enabled to be pressed.");
        button.Focus();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        HeadlessUi.Flush();
    }

    internal static string[] VisibleTexts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty)];
}
