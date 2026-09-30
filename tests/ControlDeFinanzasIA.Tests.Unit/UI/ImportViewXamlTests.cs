using System.Text.RegularExpressions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Content checks on <c>ExpensesView.axaml</c> and <c>ImportView.axaml</c> (F-020, S6b): there is no headless
/// Avalonia harness, so the structure of the import block is asserted on the source files (precedent:
/// <see cref="SettingsViewBackupBlockTests"/>). Compiled bindings are proven by the solution build itself.
/// </summary>
public sealed class ImportViewXamlTests
{
    private const string _hostMarker = "<views:ImportView DataContext=\"{Binding Import}\"";

    private static string ReadUi(params string[] relative)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ControlDeFinanzasIA.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory!.FullName, "src", "ControlDeFinanzasIA.UI", .. relative]));
    }

    private static string ExpensesView() => ReadUi("Views", "ExpensesView.axaml");

    private static string ImportView() => ReadUi("Views", "ImportView.axaml");

    private static string HostBlock(string xaml)
    {
        int start = xaml.IndexOf("Strings.ImportButton", StringComparison.Ordinal);
        int end = xaml.IndexOf("<ItemsControl ItemsSource=\"{Binding Expenses}\">", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "The import host block is missing.");
        return xaml.Substring(start, end - start);
    }

    [Fact]
    public void Toolbar_HasFourColumns_AndTheImportButtonExistsOnceNextToExport()
    {
        string xaml = ExpensesView();

        Assert.Single(Regex.Matches(xaml, "ColumnDefinitions=\"\\*,Auto,Auto,Auto\""));
        Assert.Single(Regex.Matches(xaml, "Strings\\.ImportButton\\b"));
        Assert.Single(Regex.Matches(xaml, "\\{Binding OpenImportCommand\\}"));

        int pdf = xaml.IndexOf("Strings.ExportPdfButton", StringComparison.Ordinal);
        int excel = xaml.IndexOf("Strings.ExportExcelButton", StringComparison.Ordinal);
        int import = xaml.IndexOf("Strings.ImportButton", StringComparison.Ordinal);
        Assert.True(pdf < excel && excel < import);
        Assert.Contains("Grid.Column=\"3\"", xaml.Substring(excel), StringComparison.Ordinal);
    }

    [Fact]
    public void ImportPanel_IsHostedOnce_BoundToImport_AndReplacesTheListWhileOpen()
    {
        string xaml = ExpensesView();

        Assert.Single(Regex.Matches(xaml, Regex.Escape(_hostMarker)));
        Assert.Single(Regex.Matches(xaml, "IsVisible=\"\\{Binding IsImportOpen\\}\""));
        Assert.Single(Regex.Matches(xaml, "IsVisible=\"\\{Binding !IsImportOpen\\}\""));

        int panel = xaml.IndexOf("IsVisible=\"{Binding IsImportOpen}\"", StringComparison.Ordinal);
        int list = xaml.IndexOf("IsVisible=\"{Binding !IsImportOpen}\"", StringComparison.Ordinal);
        int expenses = xaml.IndexOf("<ItemsControl ItemsSource=\"{Binding Expenses}\">", StringComparison.Ordinal);
        int empty = xaml.IndexOf("No hay gastos registrados.", StringComparison.Ordinal);
        Assert.True(panel < list && list < expenses && expenses < empty);
        Assert.Contains("{Binding CloseImportCommand}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding HasImport}", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpensesImportBlock_HasNoHardcodedTextLiterals()
    {
        string block = HostBlock(ExpensesView());

        MatchCollection literals = Regex.Matches(block, "\\b(Text|Content|Watermark)=\"([^\"]*)\"");

        Assert.NotEmpty(literals);
        Assert.All(literals, match => Assert.StartsWith("{", match.Groups[2].Value, StringComparison.Ordinal));
        Assert.DoesNotContain("Click=", block, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportView_UsesCompiledBindingsToItsViewModel()
    {
        string xaml = ImportView();

        Assert.Contains("x:DataType=\"vm:ImportViewModel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:CompileBindings=\"True\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportView_BindsEveryCommandAndEveryMappingColumn()
    {
        string xaml = ImportView();

        foreach (string command in new[] { "ConfirmCommand", "CancelCommand", "CloseCommand", "ToggleMappingCommand" })
        {
            Assert.Single(Regex.Matches(xaml, "\\{Binding " + command + "\\}"));
        }

        foreach (string column in new[] { "Date", "Type", "Description", "Category", "Amount", "Currency", "PaymentMethod" })
        {
            Assert.Single(Regex.Matches(xaml, "SelectedItem=\"\\{Binding Selected" + column + "Column\\}\""));
            Assert.Single(Regex.Matches(xaml, "Strings\\.ImportField" + column + "\\b"));
        }

        foreach (string option in new[] { "SelectedDelimiter", "DecimalStyle", "DateFormat", "DefaultType", "DefaultCurrency", "SignMode" })
        {
            Assert.Single(Regex.Matches(xaml, "SelectedValue=\"\\{Binding " + option + "\\}\""));
        }

        foreach (string toggle in new[] { "SkipDuplicates", "CreateMissingCategories", "OnlyProblems" })
        {
            Assert.Single(Regex.Matches(xaml, "IsChecked=\"\\{Binding " + toggle + "\\}\""));
        }

        foreach (string collection in new[] { "VisibleRows", "ReasonSummary", "CategoriesToCreate", "Banners" })
        {
            Assert.Single(Regex.Matches(xaml, "ItemsSource=\"\\{Binding " + collection + "\\}\""));
        }
    }

    [Fact]
    public void ImportView_ShowsTheProgressBar_TheStatusAndTheDonePanel()
    {
        string xaml = ImportView();

        Assert.Contains("ProgressBar IsIndeterminate=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsBusy}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding StatusMessage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Classes.export-error=\"{Binding IsError}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsDonePanelVisible}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DoneMessage}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsConfirmVisible}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding BackupRecommendation}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportView_HasNoHardcodedTextLiterals_NoClickHandlers_AndNoHardcodedColors()
    {
        string xaml = ImportView();

        MatchCollection literals = Regex.Matches(xaml, "\\b(Text|Content|Watermark|PlaceholderText|ToolTip\\.Tip)=\"([^\"]*)\"");

        Assert.NotEmpty(literals);
        Assert.All(literals, match => Assert.StartsWith("{", match.Groups[2].Value, StringComparison.Ordinal));
        Assert.DoesNotContain("Click=", xaml, StringComparison.Ordinal);
        Assert.Empty(Regex.Matches(xaml, "\\b(Foreground|Background|BorderBrush)=\"#"));
    }

    [Fact]
    public void ImportViewCodeBehind_ContainsOnlyInitializeComponent()
    {
        string code = ReadUi("Views", "ImportView.axaml.cs");

        Assert.Single(Regex.Matches(code, "InitializeComponent\\(\\);"));
        Assert.DoesNotContain("async", code, StringComparison.Ordinal);
        Assert.DoesNotContain("+=", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DataContext", code, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(code, "\\bpublic\\s+\\w+\\s*\\("));
    }
}
