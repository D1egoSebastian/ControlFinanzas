using System.Reflection;
using System.Xml.Linq;
using ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.RestoreBackup;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.UpdateNotificationSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.GetAppSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateLocale;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateTheme;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>F-021 S1: one version, one reader (R-1, S-1, DD-12).</summary>
public sealed class AppVersionTests
{
    private const string _expectedVersion = "1.0.0";

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [Fact]
    public void Current_IsExactlyTheReleaseVersionWithoutSourceRevisionSuffix() => Assert.Equal(_expectedVersion, AppVersionInfo.Current);

    [Fact]
    public void ExactlyOneVersionElementExistsAcrossPropsAndCsprojFiles()
    {
        string[] files = [.. Directory.EnumerateFiles(RepoRoot.Path, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".props", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                && !IsBuildOutput(f))];

        Assert.Contains(files, f => f.EndsWith("Directory.Build.props", StringComparison.Ordinal));

        var versions = files
            .SelectMany(f => XDocument.Load(f).Descendants().Where(e => e.Name.LocalName == "Version").Select(e => (File: f, e.Value)))
            .ToList();

        (string File, string Value) only = Assert.Single(versions);
        Assert.EndsWith("Directory.Build.props", only.File, StringComparison.Ordinal);
        Assert.Equal(_expectedVersion, only.Value);
    }

    [Fact]
    public void SourceRevisionSuffixIsDisabledInTheProps()
    {
        var props = XDocument.Load(RepoRoot.Combine("Directory.Build.props"));

        Assert.Equal(
            "false",
            props.Descendants().Single(e => e.Name.LocalName == "IncludeSourceRevisionInInformationalVersion").Value);
    }

    [Fact]
    public void SettingsViewModelAppVersionUsesTheSharedReader()
    {
        var store = new InMemorySettingsStore();
        var vm = new SettingsViewModel(
            new GetAppSettingsQuery(store),
            new UpdateThemeUseCase(store, NullLogger<UpdateThemeUseCase>.Instance),
            new UpdateLocaleUseCase(store, NullLogger<UpdateLocaleUseCase>.Instance),
            new UpdateAiSettingsUseCase(store, new UpdateAiSettingsValidator(), NullLogger<UpdateAiSettingsUseCase>.Instance),
            new TestAiConnectionUseCase(store, new FakeLlmClientFactory(), NullLogger<TestAiConnectionUseCase>.Instance),
            new GetNotificationSettingsQuery(store),
            new UpdateNotificationSettingsUseCase(store),
            new FakeNotificationCenter(),
            NullLogger<SettingsViewModel>.Instance);

        Assert.Equal(AppVersionInfo.Current, vm.AppVersion);
    }

    [Fact]
    public void BackupViewModelDefaultManifestVersionUsesTheSharedReader()
    {
        var service = new FakeBackupService();
        var vm = new BackupSettingsViewModel(
            new CreateBackupUseCase(service, NullLogger<CreateBackupUseCase>.Instance),
            new InspectBackupUseCase(service, NullLogger<InspectBackupUseCase>.Instance),
            new RestoreBackupUseCase(service, NullLogger<RestoreBackupUseCase>.Instance),
            new FakeFileSaveDialog(),
            new FakeFileOpenDialog(),
            new FakeNotificationCenter(),
            new FakeAppRestartService(),
            new FakeClock(),
            NullLogger<BackupSettingsViewModel>.Instance);

        object? manifestVersion = typeof(BackupSettingsViewModel)
            .GetField("_appVersion", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(vm);

        Assert.Equal(AppVersionInfo.Current, manifestVersion);
    }

    [Fact]
    public void OnlyAppVersionInfoReadsTheInformationalVersionAttribute()
    {
        string src = RepoRoot.Combine("src", "ControlDeFinanzasIA.UI");
        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .Where(f => File.ReadAllText(f).Contains("AssemblyInformationalVersionAttribute", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .ToList();

        Assert.Equal(["AppVersionInfo.cs"], offenders);
    }
}
