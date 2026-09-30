using System.Globalization;
using System.Reflection;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Services;
using ControlDeFinanzasIA.UI.ViewModels;

namespace ControlDeFinanzasIA.Tests.Unit.ViewModels;

/// <summary>
/// F-021 S4 (R-10, R-11, S-20..S-22, T5, T6): the REAL <see cref="ErrorOverlayViewModel"/> with a fake clipboard.
/// </summary>
public sealed class ErrorOverlayViewModelTests
{
    private static readonly Guid _first = Guid.Parse("3f9a21c7-0b5d-4e0a-9d1e-7c2b8a4f6e10");
    private static readonly Guid _second = Guid.Parse("a1b2c3d4-0000-4000-8000-00000000abcd");
    private static readonly DateTime _firstAt = new(2026, 9, 30, 14, 3, 11, DateTimeKind.Utc);
    private static readonly DateTime _secondAt = new(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public async Task CopyAsync_SendsExactlyTheInvariantRecord_ForTwoDifferentErrors()
    {
        var clipboard = new FakeClipboardService();
        var vm = new ErrorOverlayViewModel(clipboard, "1.0.0");

        vm.Present(_first, _firstAt);
        await vm.CopyCommand.ExecuteAsync(null);
        Assert.Equal(
            "ControlDeFinanzasIA 1.0.0\nError ID: 3f9a21c7 (3f9a21c7-0b5d-4e0a-9d1e-7c2b8a4f6e10)\nTime (UTC): 2026-09-30T14:03:11Z",
            clipboard.LastText);

        var other = new ErrorOverlayViewModel(clipboard, "2.4.1");
        other.Present(_second, _secondAt);
        await other.CopyCommand.ExecuteAsync(null);
        Assert.Equal(
            "ControlDeFinanzasIA 2.4.1\nError ID: a1b2c3d4 (a1b2c3d4-0000-4000-8000-00000000abcd)\nTime (UTC): 2027-01-02T03:04:05Z",
            clipboard.LastText);
        Assert.Equal(2, clipboard.Calls.Count);
    }

    [Fact]
    public async Task CopyText_CarriesNoStackMessageTypePathOrSeededUserData()
    {
        var clipboard = new FakeClipboardService();
        var vm = new ErrorOverlayViewModel(clipboard, "1.0.0");
        string[] seeded = ["Supermercado Nacional", "RD$ 1,250.75", "C:\\Users\\diego\\AppData", "NullReferenceException", "boom message"];

        // The exception and the user data exist only in the caller; the overlay state never receives them.
        vm.Present(_first, _firstAt);
        await vm.CopyCommand.ExecuteAsync(null);

        string text = Assert.IsType<string>(clipboard.LastText);
        Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
        foreach (string item in seeded)
        {
            Assert.DoesNotContain(item, text, StringComparison.Ordinal);
        }

        Assert.Equal(3, text.Split('\n').Length);
    }

    [Fact]
    public async Task CopyAsync_SetsTheCopiedFlag_AndAFailingClipboardLeavesItFalseWithoutThrowing()
    {
        var ok = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
        ok.Present(_first, _firstAt);
        Assert.False(ok.IsCopied);
        await ok.CopyCommand.ExecuteAsync(null);
        Assert.True(ok.IsCopied);

        var failing = new ErrorOverlayViewModel(new ThrowingClipboard(), "1.0.0");
        failing.Present(_first, _firstAt);
        await failing.CopyCommand.ExecuteAsync(null);
        Assert.False(failing.IsCopied);
        Assert.True(failing.IsOpen);
    }

    [Fact]
    public void Present_WhileOpen_CountsAdditionalErrors_KeepsTheFirstId_AndIsLocalized()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            var vm = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
            Assert.Null(vm.AdditionalText);

            vm.Present(_first, _firstAt);
            Assert.Null(vm.AdditionalText);

            vm.Present(_second, _secondAt);
            Assert.Equal(1, vm.AdditionalCount);
            Assert.Equal(_first, vm.CorrelationId);
            Assert.Equal(_firstAt, vm.OccurredAtUtc);
            Assert.Equal("3f9a21c7", vm.ShortId);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Assert.Equal("+1 more", vm.AdditionalText);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-DO");
            Assert.Equal("+1 más", vm.AdditionalText);

            vm.Present(Guid.NewGuid(), _secondAt);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Assert.Equal("+2 more", vm.AdditionalText);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Close_ResetsEveryField_AndALaterPresentStartsFresh()
    {
        var vm = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");
        vm.Present(_first, _firstAt);
        vm.Present(_second, _secondAt);
        await vm.CopyCommand.ExecuteAsync(null);
        Assert.NotNull(vm.InitialFocus);

        vm.CloseCommand.Execute(null);

        Assert.False(vm.IsOpen);
        Assert.Equal(Guid.Empty, vm.CorrelationId);
        Assert.Equal(string.Empty, vm.ShortId);
        Assert.Equal(default, vm.OccurredAtUtc);
        Assert.Equal(0, vm.AdditionalCount);
        Assert.Null(vm.AdditionalText);
        Assert.False(vm.IsCopied);
        Assert.Null(vm.InitialFocus);
        Assert.Equal(string.Empty, vm.CopyText);

        vm.Present(_second, _secondAt);
        Assert.True(vm.IsOpen);
        Assert.Equal("a1b2c3d4", vm.ShortId);
        Assert.Equal(0, vm.AdditionalCount);
    }

    [Fact]
    public void Present_RaisesAFreshInitialFocusToken_ForEveryOpening_ButNotForAdditionalErrors()
    {
        var vm = new ErrorOverlayViewModel(new FakeClipboardService(), "1.0.0");

        vm.Present(_first, _firstAt);
        FocusRequestToken? opened = vm.InitialFocus;
        Assert.NotNull(opened);

        vm.Present(_second, _secondAt);
        Assert.Same(opened, vm.InitialFocus);

        vm.Close();
        vm.Present(_second, _secondAt);
        Assert.NotNull(vm.InitialFocus);
        Assert.NotSame(opened, vm.InitialFocus);
    }

    [Fact]
    public void ItNeverDependsOnTheRestartService_ByConstruction()
    {
        ConstructorInfo ctor = Assert.Single(typeof(ErrorOverlayViewModel).GetConstructors());
        Assert.Equal(
            [typeof(IClipboardService), typeof(string)],
            ctor.GetParameters().Select(p => p.ParameterType).ToArray());

        FieldInfo[] fields = typeof(ErrorOverlayViewModel)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.DoesNotContain(fields, f => f.FieldType.Name.Contains("Restart", StringComparison.Ordinal));
        Assert.Throws<ArgumentNullException>(() => new ErrorOverlayViewModel(null!, "1.0.0"));
        Assert.Throws<ArgumentException>(() => new ErrorOverlayViewModel(new FakeClipboardService(), " "));
    }

    private sealed class ThrowingClipboard : IClipboardService
    {
        public Task SetTextAsync(string text) => throw new InvalidOperationException("clipboard unavailable");
    }
}
