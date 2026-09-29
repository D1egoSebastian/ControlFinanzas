using System.Text.RegularExpressions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Content checks on <c>SettingsView.axaml</c> (F-019, S6b): there is no headless Avalonia harness, so
/// the structure of the Backup block is asserted on the source file.
/// </summary>
public sealed class SettingsViewBackupBlockTests
{
    private const string _backupMarker = "DataContext=\"{Binding Backup}\"";

    private static string ReadView()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ControlDeFinanzasIA.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, "src", "ControlDeFinanzasIA.UI", "Views", "SettingsView.axaml"));
    }

    [Fact]
    public void BackupBlock_ExistsOnce_AfterNotifications_AndBeforeAbout()
    {
        string xaml = ReadView();

        int first = xaml.IndexOf(_backupMarker, StringComparison.Ordinal);
        Assert.True(first >= 0, "The Backup block is missing.");
        Assert.Equal(first, xaml.LastIndexOf(_backupMarker, StringComparison.Ordinal));

        int notifications = xaml.IndexOf("Strings.SettingsNotificationsTitle", StringComparison.Ordinal);
        int about = xaml.IndexOf("Strings.SettingsAboutTitle", StringComparison.Ordinal);
        Assert.True(notifications >= 0 && notifications < first);
        Assert.True(first < about);
        Assert.Equal(about, xaml.LastIndexOf("Strings.SettingsAboutTitle", StringComparison.Ordinal));

        // About stays the last section: no other ledger-row block follows it.
        Assert.Equal(-1, xaml.IndexOf("Classes=\"ledger-row\"", about, StringComparison.Ordinal));
    }

    [Fact]
    public void BackupBlock_UsesCompiledBindingsToItsViewModel_AndHidesWhenNull()
    {
        string xaml = ReadView();
        int start = xaml.IndexOf(_backupMarker, StringComparison.Ordinal);

        string block = xaml.Substring(start, xaml.IndexOf("Strings.SettingsAboutTitle", StringComparison.Ordinal) - start);
        string header = xaml.Substring(Math.Max(0, start - 300), 300);

        Assert.Contains("x:DataType=\"vm:BackupSettingsViewModel\"", block, StringComparison.Ordinal);
        Assert.Contains("ObjectConverters.IsNotNull", header, StringComparison.Ordinal);
        foreach (string command in new[] { "ExportCommand", "ChooseRestoreFileCommand", "ConfirmRestoreCommand", "CancelRestoreCommand", "RestartNowCommand" })
        {
            Assert.Contains("{Binding " + command + "}", block, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BackupBlock_HasNoHardcodedTextLiterals()
    {
        string xaml = ReadView();
        int start = xaml.IndexOf(_backupMarker, StringComparison.Ordinal);
        string block = xaml.Substring(start, xaml.IndexOf("Strings.SettingsAboutTitle", StringComparison.Ordinal) - start);

        MatchCollection literals = Regex.Matches(block, "\\b(Text|Content|Watermark)=\"([^\"]*)\"");

        Assert.NotEmpty(literals);
        Assert.All(literals, match => Assert.StartsWith("{", match.Groups[2].Value, StringComparison.Ordinal));
    }
}
