using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using ControlDeFinanzasIA.UI.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlDeFinanzasIA.Tests.Unit.Keyboard;

/// <summary>
/// F-021 S7a (R-14, S-25, S-31): a whole "register an expense" flow driven by headless key events only, on the REAL
/// <see cref="MainWindow"/>, the REAL <see cref="ShellViewModel"/> and <see cref="NavigationService"/> and the REAL
/// <see cref="ExpensesViewModel"/> with its real use cases, over a migrated temp-file SQLite database built by the real
/// composition root. No mouse, no direct command call, no fake repository.
/// </summary>
/// <remarks>
/// Lives in the Unit project (not Integration) because the single shared headless Avalonia session is defined there
/// and the Integration project has no Avalonia.Headless reference (a csproj/package change is out of scope for F-021).
/// </remarks>
[Collection(HeadlessUi.CollectionName)]
public sealed class KeyboardOnlyExpenseFlowTests
{
    private sealed class TempPathResolver(string directory) : IPathResolver
    {
        public string DataDirectory => directory;

        public string DatabaseFilePath => Path.Combine(directory, "data.db");
    }

    private static ServiceProvider BuildRealComposition(string directory)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());

        var resolver = new TempPathResolver(directory);
        services.RemoveAll<IPathResolver>();
        services.AddSingleton<IPathResolver>(resolver);
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={resolver.DatabaseFilePath}"));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false, ValidateOnBuild = false });
    }

    private static AppDbContext StandaloneContext(string databasePath) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={databasePath};Pooling=False").Options);

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        for (int i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(10);
            HeadlessUi.Flush();
        }

        Assert.True(condition(), $"Timed out waiting for: {what}.");
    }

    private static IInputElement? Focused(Window window) => window.FocusManager!.GetFocusedElement();

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        HeadlessUi.Flush();
    }

    [Theory]
    [InlineData("es-DO")]
    [InlineData("en")]
    public async Task ARegisteredExpense_NeedsNoMouseAndNoDirectCommandCall(string culture)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"kbd-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string databasePath = Path.Combine(directory, "data.db");
        CultureInfo previous = CultureInfo.CurrentUICulture;

        try
        {
            (int Stored, string Description, decimal Amount, bool OnScreen, bool FocusBackOnDescription, bool PassedDate, string Section) result =
                await HeadlessUi.OnUiThread(async () =>
                {
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                    using ServiceProvider provider = BuildRealComposition(directory);
                    using (AppDbContext migrator = StandaloneContext(databasePath))
                    {
                        await migrator.Database.MigrateAsync();
                    }

                    ShellViewModel shell = provider.GetRequiredService<ShellViewModel>();
                    var window = new MainWindow { DataContext = shell, Width = 1100, Height = 900 };
                    window.Show();
                    HeadlessUi.Flush();
                    try
                    {
                        // Start on another section (Ctrl+5 = Debts) so Ctrl+N has to navigate.
                        Press(window, PhysicalKey.Digit5, RawInputModifiers.Control);
                        await WaitUntilAsync(() => shell.Current is DebtsViewModel, "Ctrl+5 to open Debts");
                        string section = shell.Current!.GetType().Name;

                        Press(window, PhysicalKey.N, RawInputModifiers.Control);
                        await WaitUntilAsync(() => shell.Current is ExpensesViewModel, "Ctrl+N to open Expenses");
                        var expenses = (ExpensesViewModel)shell.Current!;
                        await WaitUntilAsync(() => expenses.HasLoaded && expenses.Categories.Count > 0, "the expense form data");

                        TextBox description = window.GetVisualDescendants().OfType<TextBox>()
                            .Single(t => t.Watermark == Strings.ExpensesDescriptionPlaceholder);
                        TextBox amount = window.GetVisualDescendants().OfType<TextBox>()
                            .Single(t => t.Watermark == Strings.ExpensesAmountPlaceholder);
                        await WaitUntilAsync(() => ReferenceEquals(Focused(window), description), "Description to receive focus");

                        window.KeyTextInput("Almuerzo");
                        HeadlessUi.Flush();

                        Press(window, PhysicalKey.Tab);
                        Assert.Same(amount, Focused(window));
                        window.KeyTextInput("250");
                        HeadlessUi.Flush();

                        Press(window, PhysicalKey.Tab);
                        ComboBox category = Assert.IsType<ComboBox>(Focused(window));
                        Assert.Equal(Strings.ExpensesCategoryPlaceholder, category.PlaceholderText);
                        Press(window, PhysicalKey.ArrowDown);
                        await WaitUntilAsync(() => expenses.SelectedCategoryId is not null, "a category chosen with the arrow key");

                        DatePicker date = window.GetVisualDescendants().OfType<DatePicker>().First();
                        Button register = window.GetVisualDescendants().OfType<Button>()
                            .Single(b => ReferenceEquals(b.Command, expenses.RegisterExpenseCommand));
                        bool passedDate = false;
                        for (int i = 0; i < 25 && !ReferenceEquals(Focused(window), register); i++)
                        {
                            Press(window, PhysicalKey.Tab);
                            passedDate |= Focused(window) is Visual v && date.IsVisualAncestorOf(v);
                        }

                        Assert.Same(register, Focused(window));
                        Press(window, PhysicalKey.Enter);
                        await WaitUntilAsync(() => expenses.Expenses.Any(e => e.Description == "Almuerzo"), "the new expense in the list");
                        await WaitUntilAsync(() => ReferenceEquals(Focused(window), description), "focus to return to Description");

                        bool onScreen = window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Almuerzo");
                        using AppDbContext reader = StandaloneContext(databasePath);
                        List<Expense> stored = await reader.Expenses.AsNoTracking().ToListAsync();
                        Expense row = stored.Single();
                        return (stored.Count, row.Description, row.Amount.Amount, onScreen, ReferenceEquals(Focused(window), description), passedDate, section);
                    }
                    finally
                    {
                        window.Close();
                        HeadlessUi.Flush();
                        SqliteConnection.ClearAllPools();
                    }
                });

            Assert.Equal("DebtsViewModel", result.Section);
            Assert.Equal(1, result.Stored);
            Assert.Equal("Almuerzo", result.Description);
            Assert.Equal(250m, result.Amount);
            Assert.True(result.OnScreen, "The new expense row is visible in the list.");
            Assert.True(result.PassedDate, "Tab reached the date picker on the way to the button.");
            Assert.True(result.FocusBackOnDescription, "Focus returns to Description for the next entry.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }
}
