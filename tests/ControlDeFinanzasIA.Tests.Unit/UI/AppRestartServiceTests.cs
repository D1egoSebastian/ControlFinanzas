using System.Diagnostics;
using ControlDeFinanzasIA.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// <see cref="AppRestartService"/> with the process-launch and shutdown seams replaced, so no
/// test ever spawns a process or closes a window (F-019, DD-8, R-19).
/// </summary>
public sealed class AppRestartServiceTests
{
    private const string _exe = @"C:\Apps\ControlDeFinanzasIA.UI.exe";

    private static AppRestartService Create(
        string? processPath,
        string? entryAssemblyPath,
        Func<ProcessStartInfo, bool> launch,
        Func<bool> shutdown) =>
        new(processPath, entryAssemblyPath, launch, shutdown, NullLogger<AppRestartService>.Instance);

    [Fact]
    public void BuildStartInfo_SelfContainedHost_RelaunchesTheExecutableWithoutArguments()
    {
        ProcessStartInfo? info = AppRestartService.BuildStartInfo(_exe, @"C:\Apps\ControlDeFinanzasIA.UI.dll");

        Assert.NotNull(info);
        Assert.Equal(_exe, info!.FileName);
        Assert.Empty(info.ArgumentList);
        Assert.False(info.UseShellExecute);
        Assert.Equal(@"C:\Apps", info.WorkingDirectory);
    }

    [Theory]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData(@"C:\Program Files\dotnet\DOTNET.EXE")]
    [InlineData("/usr/share/dotnet/dotnet")]
    public void BuildStartInfo_DotnetHost_AddsTheEntryAssemblyAsFirstArgument(string host)
    {
        ProcessStartInfo? info = AppRestartService.BuildStartInfo(host, @"C:\Apps\ControlDeFinanzasIA.UI.dll");

        Assert.NotNull(info);
        Assert.Equal(host, info!.FileName);
        Assert.Equal([@"C:\Apps\ControlDeFinanzasIA.UI.dll"], info.ArgumentList);
        Assert.False(info.UseShellExecute);
    }

    [Theory]
    [InlineData(null, "x.dll")]
    [InlineData("", "x.dll")]
    [InlineData(@"C:\dotnet\dotnet.exe", null)]
    [InlineData(@"C:\dotnet\dotnet.exe", "")]
    public void BuildStartInfo_ReturnsNull_WhenTheLaunchCannotBeBuilt(string? processPath, string? entry) =>
        Assert.Null(AppRestartService.BuildStartInfo(processPath, entry));

    [Fact]
    public void TryRestart_LaunchesThenShutsDown_AndReturnsTrue()
    {
        var order = new List<string>();
        ProcessStartInfo? started = null;
        AppRestartService service = Create(
            _exe,
            "x.dll",
            info =>
            {
                order.Add("launch");
                started = info;
                return true;
            },
            () =>
            {
                order.Add("shutdown");
                return true;
            });

        bool result = service.TryRestart();

        Assert.True(result);
        Assert.Equal(["launch", "shutdown"], order);
        Assert.Equal(_exe, started!.FileName);
    }

    [Fact]
    public void TryRestart_ReturnsFalse_AndKeepsTheAppOpen_WhenTheProcessDoesNotStart()
    {
        int shutdowns = 0;
        AppRestartService service = Create(_exe, "x.dll", _ => false, () => { shutdowns++; return true; });

        Assert.False(service.TryRestart());
        Assert.Equal(0, shutdowns);
    }

    [Fact]
    public void TryRestart_ReturnsFalse_WhenTheLaunchThrows()
    {
        int shutdowns = 0;
        AppRestartService service = Create(
            _exe,
            "x.dll",
            _ => throw new InvalidOperationException("boom"),
            () => { shutdowns++; return true; });

        Assert.False(service.TryRestart());
        Assert.Equal(0, shutdowns);
    }

    [Fact]
    public void TryRestart_ReturnsFalse_WhenShutdownThrows()
    {
        AppRestartService service = Create(_exe, "x.dll", _ => true, () => throw new InvalidOperationException("boom"));

        Assert.False(service.TryRestart());
    }

    [Fact]
    public void TryRestart_ReturnsFalse_WhenThereIsNoLifetimeToShutDown()
    {
        AppRestartService service = Create(_exe, "x.dll", _ => true, () => false);

        Assert.False(service.TryRestart());
    }

    [Fact]
    public void TryRestart_ReturnsFalse_WithoutLaunching_WhenTheProcessPathIsUnknown()
    {
        int launches = 0;
        AppRestartService service = Create(null, "x.dll", _ => { launches++; return true; }, () => true);

        Assert.False(service.TryRestart());
        Assert.Equal(0, launches);
    }
}
