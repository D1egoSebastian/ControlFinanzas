using System.Globalization;
using Avalonia.Input;
using ControlDeFinanzasIA.UI.Input;
using ControlDeFinanzasIA.UI.Resources;

namespace ControlDeFinanzasIA.Tests.Unit.Input;

/// <summary>F-021 S7a (R-12, S-24, T10, DD-6): the REAL <see cref="KeyboardShortcutCatalog"/>.</summary>
public sealed class KeyboardShortcutCatalogTests
{
    private static KeyboardShortcut Find(ShortcutAction action, int? section = null) =>
        KeyboardShortcutCatalog.All.Single(s => s.Action == action && s.SectionIndex == section);

    [Fact]
    public void All_HasExactlyFifteenEntries_OneForEachDocumentedGesture()
    {
        IReadOnlyList<KeyboardShortcut> all = KeyboardShortcutCatalog.All;

        Assert.Equal(15, all.Count);
        Assert.Single(all, s => s.Action == ShortcutAction.NewExpense);
        Assert.Single(all, s => s.Action == ShortcutAction.NewIncome);
        Assert.Equal(10, all.Count(s => s.Action == ShortcutAction.GoToSection));
        Assert.Single(all, s => s.Action == ShortcutAction.OpenSettings);
        Assert.Single(all, s => s.Action == ShortcutAction.ToggleNotifications);
        Assert.Single(all, s => s.Action == ShortcutAction.Escape);

        Assert.Equal((Key.N, KeyModifiers.Control), (Find(ShortcutAction.NewExpense).Key, Find(ShortcutAction.NewExpense).Modifiers));
        Assert.Equal((Key.N, KeyModifiers.Control | KeyModifiers.Shift), (Find(ShortcutAction.NewIncome).Key, Find(ShortcutAction.NewIncome).Modifiers));
        Assert.Equal((Key.OemComma, KeyModifiers.Control), (Find(ShortcutAction.OpenSettings).Key, Find(ShortcutAction.OpenSettings).Modifiers));
        Assert.Equal((Key.B, KeyModifiers.Control | KeyModifiers.Shift), (Find(ShortcutAction.ToggleNotifications).Key, Find(ShortcutAction.ToggleNotifications).Modifiers));
        Assert.Equal((Key.Escape, KeyModifiers.None), (Find(ShortcutAction.Escape).Key, Find(ShortcutAction.Escape).Modifiers));

        Key[] digitKeys = [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.D0];
        int[] digits = [1, 2, 3, 4, 5, 6, 7, 8, 9, 0];
        for (int i = 0; i < 10; i++)
        {
            KeyboardShortcut section = Find(ShortcutAction.GoToSection, digits[i]);
            Assert.Equal(digitKeys[i], section.Key);
            Assert.Equal(KeyModifiers.Control, section.Modifiers);
        }
    }

    [Fact]
    public void Gestures_AreUnique_OnEveryPlatform()
    {
        foreach (bool mac in new[] { false, true })
        {
            var gestures = KeyboardShortcutCatalog.ForPlatform(mac).Select(s => (s.Key, s.Modifiers)).ToList();
            Assert.Equal(mac ? 29 : 15, gestures.Count);
            Assert.Equal(gestures.Count, gestures.Distinct().Count());
        }
    }

    [Fact]
    public void ForPlatform_OnWindowsAndLinux_HasNoMetaEntry_AndEqualsAll()
    {
        IReadOnlyList<KeyboardShortcut> platform = KeyboardShortcutCatalog.ForPlatform(false);

        Assert.Equal(15, platform.Count);
        Assert.DoesNotContain(platform, s => s.Modifiers.HasFlag(KeyModifiers.Meta));
        Assert.Equal(KeyboardShortcutCatalog.All, platform);
    }

    [Fact]
    public void ForPlatform_OnMacOS_AddsExactlyOneMetaDuplicateForEveryModifiedEntry()
    {
        IReadOnlyList<KeyboardShortcut> mac = KeyboardShortcutCatalog.ForPlatform(true);
        var ctrl = KeyboardShortcutCatalog.All.Where(s => s.Modifiers.HasFlag(KeyModifiers.Control)).ToList();
        var meta = mac.Where(s => s.Modifiers.HasFlag(KeyModifiers.Meta)).ToList();

        Assert.Equal(14, ctrl.Count);
        Assert.Equal(ctrl.Count, meta.Count);
        Assert.Equal(KeyboardShortcutCatalog.All.Count + meta.Count, mac.Count);
        foreach (KeyboardShortcut original in ctrl)
        {
            KeyboardShortcut duplicate = Assert.Single(
                meta,
                m => m.Action == original.Action && m.SectionIndex == original.SectionIndex && m.Key == original.Key);
            Assert.Equal((original.Modifiers & ~KeyModifiers.Control) | KeyModifiers.Meta, duplicate.Modifiers);
            Assert.Equal(original.DescriptionKey, duplicate.DescriptionKey);
        }

        // Escape has no modifier, so it has no Meta twin (Meta+Esc would be a new, undocumented gesture).
        Assert.DoesNotContain(meta, m => m.Action == ShortcutAction.Escape);
        Assert.Contains(mac, s => s.Action == ShortcutAction.Escape && s.Modifiers == KeyModifiers.None);
    }

    [Fact]
    public void Format_OnWindowsAndLinux_JoinsTheLabelsWithAPlus()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            Assert.Equal("Ctrl+N", KeyboardShortcutCatalog.Format(Find(ShortcutAction.NewExpense), false));
            Assert.Equal("Ctrl+Shift+N", KeyboardShortcutCatalog.Format(Find(ShortcutAction.NewIncome), false));
            Assert.Equal("Ctrl+1", KeyboardShortcutCatalog.Format(Find(ShortcutAction.GoToSection, 1), false));
            Assert.Equal("Ctrl+0", KeyboardShortcutCatalog.Format(Find(ShortcutAction.GoToSection, 0), false));
            Assert.Equal("Ctrl+,", KeyboardShortcutCatalog.Format(Find(ShortcutAction.OpenSettings), false));
            Assert.Equal("Ctrl+Shift+B", KeyboardShortcutCatalog.Format(Find(ShortcutAction.ToggleNotifications), false));
            Assert.Equal("Esc", KeyboardShortcutCatalog.Format(Find(ShortcutAction.Escape), false));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Format_OnMacOS_UsesSymbols()
    {
        Assert.Equal("⌘N", KeyboardShortcutCatalog.Format(Find(ShortcutAction.NewExpense), true));
        Assert.Equal("⌘⇧N", KeyboardShortcutCatalog.Format(Find(ShortcutAction.NewIncome), true));
        Assert.Equal("⌘3", KeyboardShortcutCatalog.Format(Find(ShortcutAction.GoToSection, 3), true));
        Assert.Equal("⌘,", KeyboardShortcutCatalog.Format(Find(ShortcutAction.OpenSettings), true));
        Assert.Equal("⌘⇧B", KeyboardShortcutCatalog.Format(Find(ShortcutAction.ToggleNotifications), true));
        Assert.Equal("Esc", KeyboardShortcutCatalog.Format(Find(ShortcutAction.Escape), true));
    }

    [Fact]
    public void Format_TakesTheShiftLabelFromTheResources()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-DO");
            string spanish = KeyboardShortcutCatalog.Format(Find(ShortcutAction.NewIncome), false);
            Assert.Equal($"Ctrl+{Strings.ShortcutKeyShift}+N", spanish);
            Assert.False(string.IsNullOrWhiteSpace(Strings.ShortcutKeyShift));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es-DO")]
    public void EveryDescriptionKey_ResolvesToANonEmptyString_InBothCultures(string culture)
    {
        var info = CultureInfo.GetCultureInfo(culture);
        Assert.Equal(29, KeyboardShortcutCatalog.ForPlatform(true).Count);

        foreach (KeyboardShortcut shortcut in KeyboardShortcutCatalog.ForPlatform(true))
        {
            string? text = Strings.ResourceManager.GetString(shortcut.DescriptionKey, info);
            Assert.False(string.IsNullOrWhiteSpace(text), $"{shortcut.DescriptionKey} is empty in {culture}.");
        }
    }

    [Fact]
    public void TheEnglishAndSpanishDescriptionsDiffer()
    {
        var en = CultureInfo.GetCultureInfo("en");
        var es = CultureInfo.GetCultureInfo("es-DO");
        Assert.Equal(6, KeyboardShortcutCatalog.All.Select(s => s.DescriptionKey).Distinct().Count());

        foreach (string key in KeyboardShortcutCatalog.All.Select(s => s.DescriptionKey).Distinct())
        {
            Assert.NotEqual(Strings.ResourceManager.GetString(key, en), Strings.ResourceManager.GetString(key, es));
        }
    }
}
