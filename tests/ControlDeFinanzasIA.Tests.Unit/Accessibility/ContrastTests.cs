using System.Globalization;
using System.Xml.Linq;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Tests.Unit.Packaging;
using ControlDeFinanzasIA.UI.Views.Charts;

namespace ControlDeFinanzasIA.Tests.Unit.Accessibility;

/// <summary>
/// WCAG 2.x contrast of the REAL palettes (<c>Tokens.Light.axaml</c> and <c>Tokens.Dark.axaml</c> parsed) and of the pairs
/// the REAL styles and views use. Thresholds: 4.5:1 for text, 3:1 for focus rings, input boundaries and graphical fills.
/// No failing pair is excluded: a failure is fixed in the tokens or the style, not in this list. Pre-1.0 slice E replaced
/// the dark "Libro Mayor" palette with the light "Sand and Cobalt" palette; the dark-mode slice added the Dark variant of
/// the same palette and EVERY assertion below runs for BOTH variants with the same thresholds.
/// </summary>
public sealed class ContrastTests
{
    private const double _textMinimum = 4.5;
    private const double _graphicMinimum = 3.0;

    private static readonly string _uiRoot = RepoRoot.Combine("src", "ControlDeFinanzasIA.UI");

    private static readonly XNamespace _xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The two theme variants; every contrast assertion runs for each of them.</summary>
    private static readonly string[] _variants = ["Light", "Dark"];

    /// <summary>Foreground resources a view or style may use for text; every one has its pairs asserted below.</summary>
    private static readonly string[] _textBrushes =
    [
        "TextBrush", "TextSecondaryBrush", "TextMutedBrush", "AccentBrush", "OnAccentBrush", "IncomeBrush", "ExpenseBrush", "WarningBrush",
    ];

    /// <summary>Surfaces that text can sit on.</summary>
    private static readonly string[] _surfaces = ["Canvas", "Surface", "SurfaceMuted"];

    public static TheoryData<string> Variants()
    {
        var data = new TheoryData<string>();
        foreach (string variant in _variants)
        {
            data.Add(variant);
        }

        return data;
    }

    public static TheoryData<string, string, string, string, double> TokenPairs()
    {
        var data = new TheoryData<string, string, string, string, double>();
        foreach (string variant in _variants)
        {
            foreach ((string pair, string foreground, string background, double minimum) in PairsOf())
            {
                data.Add(variant, pair, foreground, background, minimum);
            }
        }

        return data;
    }

    private static IEnumerable<(string Pair, string Foreground, string Background, double Minimum)> PairsOf()
    {
        foreach (string foreground in new[] { "Text", "TextSecondary", "TextMuted", "Accent", "Income", "Expense", "Warning" })
        {
            foreach (string background in _surfaces)
            {
                yield return ($"{foreground} text on {background}", foreground, background, _textMinimum);
            }
        }

        // Soft semantic washes (chips, notices, selected sidebar item) carry their own text colour.
        yield return ("Text on AccentSoft", "Text", "AccentSoft", _textMinimum);
        yield return ("Accent text on AccentSoft", "Accent", "AccentSoft", _textMinimum);
        yield return ("Income text on IncomeSoft", "Income", "IncomeSoft", _textMinimum);
        yield return ("Expense text on ExpenseSoft", "Expense", "ExpenseSoft", _textMinimum);
        yield return ("Warning text on WarningSoft", "Warning", "WarningSoft", _textMinimum);
        yield return ("TextSecondary on AccentSoft", "TextSecondary", "AccentSoft", _textMinimum);

        // Filled buttons: the label on the Accent fill and on its hover fill.
        yield return ("OnAccent label on the Accent button fill", "OnAccent", "Accent", _textMinimum);
        yield return ("OnAccent label on the AccentHover button fill", "OnAccent", "AccentHover", _textMinimum);
        yield return ("OnAccent label on the AccentPressed button fill", "OnAccent", "AccentPressed", _textMinimum);
        yield return ("OnAccent count on the Expense notification badge", "OnAccent", "Expense", _textMinimum);

        // Focus ring (3:1) against every surface it can touch, including the primary fill it is drawn over.
        foreach (string background in new[] { "Canvas", "Surface", "SurfaceMuted", "Accent", "AccentSoft" })
        {
            yield return ($"Focus ring on {background}", "FocusRing", background, _graphicMinimum);
        }

        // Input boundaries and graphical objects (3:1).
        yield return ("Input border on Surface", "InputBorder", "Surface", _graphicMinimum);
        yield return ("Input border on Canvas", "InputBorder", "Canvas", _graphicMinimum);
        yield return ("Accent focus border on Surface", "Accent", "Surface", _graphicMinimum);
        yield return ("Income progress/amount fill on Surface", "Income", "Surface", _graphicMinimum);
        yield return ("Expense progress/amount fill on Surface", "Expense", "Surface", _graphicMinimum);
        yield return ("Warning progress fill on Surface", "WarningFill", "Surface", _graphicMinimum);
    }

    [Theory]
    [MemberData(nameof(TokenPairs))]
    public void TheTokenPair_MeetsItsThreshold(string variant, string pair, string foreground, string background, double minimum)
    {
        Dictionary<string, Rgb> palette = Palette(variant);

        double ratio = Ratio(palette[foreground], palette[background]);

        Assert.True(ratio >= minimum, $"[{variant}] {pair}: {ratio:0.00}:1 is below {minimum}:1.");
    }

    [Fact]
    public void TheLightPalette_IsLight_AndTheCanvasIsNotPlainWhite()
    {
        Dictionary<string, Rgb> palette = Palette("Light");

        Assert.True(palette["Canvas"].Luminance > 0.8, "The canvas is a light surface.");
        Assert.True(palette["Surface"].Luminance > palette["Canvas"].Luminance, "Cards sit above the canvas (lighter).");
        Assert.True(palette["Text"].Luminance < 0.05, "Body text is near-ink.");
    }

    [Fact]
    public void TheDarkPalette_IsDark_WithRaisedCards_AndLightText()
    {
        Dictionary<string, Rgb> palette = Palette("Dark");

        Assert.True(palette["Canvas"].Luminance < 0.02, "The canvas is a deep warm-ink surface.");
        Assert.True(palette["Surface"].Luminance > palette["Canvas"].Luminance, "Cards sit above the canvas (slightly lighter).");
        Assert.True(palette["SurfaceMuted"].Luminance > palette["Surface"].Luminance, "Hover and muted fills are lighter than the card.");
        Assert.True(palette["Text"].Luminance > 0.6, "Body text is a warm off-white.");
        Assert.True(palette["Canvas"].B < palette["Canvas"].R + 0.02, "The canvas is warm-neutral, not blue-black.");
    }

    [Fact]
    public void BothVariants_DefineExactlyTheSameColorTokens()
    {
        string[] light = [.. Palette("Light").Keys.Order(StringComparer.Ordinal)];
        string[] dark = [.. Palette("Dark").Keys.Order(StringComparer.Ordinal)];

        Assert.Equal(light, dark);
        Assert.True(light.Length >= 20, "The semantic palette is missing from a variant.");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void EveryVariant_DefinesTheSameResourceKeys(string variant)
    {
        string[] reference = [.. ResourceKeys("Light").Order(StringComparer.Ordinal)];
        string[] keys = [.. ResourceKeys(variant).Order(StringComparer.Ordinal)];

        Assert.Equal(reference, keys);
        Assert.Contains("SurfaceBrush", keys);
        Assert.Contains("ShadowCard", keys);
        Assert.Contains("ComboBoxBackground", keys);
    }

    [Fact]
    public void TheSharedTokenFile_HoldsNoColorOrBrush_SoNothingIsLeftOutsideTheVariants()
    {
        var document = XDocument.Load(Path.Combine(_uiRoot, "Styles", "Tokens.axaml"));
        string[] hits =
        [
            .. document.Root!.Elements()
                .Where(e => e.Name.LocalName is "Color" or "SolidColorBrush" or "BoxShadows")
                .Select(e => (string?)e.Attribute(_xaml + "Key") ?? e.Name.LocalName),
        ];

        Assert.True(hits.Length == 0, "Colours belong in Tokens.Light.axaml / Tokens.Dark.axaml:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void TheDangerLabelAndTheExpenseAmount_UseTheExpenseTextToken()
    {
        string styles = File.ReadAllText(Path.Combine(_uiRoot, "Styles", "LedgerStyles.axaml"));

        Assert.Equal("{DynamicResource ExpenseBrush}", StyleSetter("Button.ledger-btn-danger", "Foreground"));
        Assert.Equal("{DynamicResource ExpenseBrush}", StyleSetter("TextBlock.ledger-amount.expense", "Foreground"));
        Assert.DoesNotContain("TerracottaBrush", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void NoStyleOrView_BindsAThemedColorToken_WithAOneShotStaticResource()
    {
        // A StaticResource is resolved once, so it would keep the colours of the variant that was active when the view was
        // created and a live theme switch would leave it behind. Every colour, brush and shadow token is a DynamicResource.
        var themed = new System.Text.RegularExpressions.Regex(@"\{StaticResource\s+(?<key>\w*(Brush|Color)|Shadow\w+)\}");
        var hits = new List<string>();
        foreach (string folder in new[] { "Views", "Controls", "Styles" })
        {
            foreach (string file in Directory.EnumerateFiles(Path.Combine(_uiRoot, folder), "*.axaml", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (System.Text.RegularExpressions.Match m in themed.Matches(lines[i]))
                    {
                        if (!Path.GetFileName(file).StartsWith("Tokens.", StringComparison.Ordinal))
                        {
                            hits.Add($"{Path.GetRelativePath(_uiRoot, file)}:{i + 1}: {m.Value}");
                        }
                    }
                }
            }
        }

        Assert.True(hits.Count == 0, "Use DynamicResource for themed tokens:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void TheAdjacentMonthCalendarDays_StayReadable_WithoutOpacity(string variant)
    {
        Dictionary<string, Rgb> palette = Palette(variant);
        string? opacity = StyleSetterOrNull("Button.calendar-day.adjacent", "Opacity");

        Assert.Null(opacity);
        Assert.Equal("{DynamicResource TextMutedBrush}", StyleSetter("Button.calendar-day.adjacent", "Foreground"));
        Assert.True(Ratio(palette["TextMuted"], palette["Surface"]) >= _textMinimum);
        Assert.True(Ratio(palette["TextMuted"], palette["Canvas"]) >= _textMinimum);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void TheSidebarPairs_MeetTextContrast_IncludingTheDigitHintAndTheCurrentWash(string variant)
    {
        Dictionary<string, Rgb> palette = Palette(variant);

        (string Name, double Ratio)[] pairs =
        [
            ("digit hint (TextMuted) on Surface", Ratio(palette["TextMuted"], palette["Surface"])),
            ("inactive label (TextSecondary) on Surface", Ratio(palette["TextSecondary"], palette["Surface"])),
            ("current label (Accent) on AccentSoft", Ratio(palette["Accent"], palette["AccentSoft"])),
            ("current digit hint (Accent) on AccentSoft", Ratio(palette["Accent"], palette["AccentSoft"])),
            ("hover label (Text) on SurfaceMuted", Ratio(palette["Text"], palette["SurfaceMuted"])),
        ];

        foreach ((string name, double ratio) in pairs)
        {
            Assert.True(ratio >= _textMinimum, $"[{variant}] {name}: {ratio:0.00}:1.");
        }

        Assert.Equal("{DynamicResource TextMutedBrush}", StyleSetter("TextBlock.sidebar-hint", "Foreground"));
        Assert.Null(StyleSetterOrNull("TextBlock.sidebar-hint", "Opacity"));
    }

    [Fact]
    public void NoTextForeground_BindsToAFillOnlyToken()
    {
        string[] fillOnly = ["WarningFillBrush", "InputBorderBrush", "DividerBrush", "AccentSoftBrush", "IncomeSoftBrush", "ExpenseSoftBrush", "WarningSoftBrush"];
        string[] hits = [.. ForegroundValues().Where(v => fillOnly.Any(f => v.Value.Contains(f, StringComparison.Ordinal))).Select(v => $"{v.Where}: {v.Value}")];

        Assert.True(hits.Length == 0, "Fill-only tokens must not be used for text:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void EveryTextForeground_IsOneOfTheTokensWhosePairsAreAssertedAbove()
    {
        string[] hits =
        [
            .. ForegroundValues()
                .Where(v => !_textBrushes.Any(b => v.Value == "{DynamicResource " + b + "}"))
                .Where(v => !v.Value.Contains("BudgetStatusBrushConverter", StringComparison.Ordinal))
                .Where(v => !v.Value.Contains("ChatMessageStateBrushConverter", StringComparison.Ordinal))
                .Where(v => !v.Value.Contains("AnomalySeverityBrushConverter", StringComparison.Ordinal))
                .Where(v => !v.Value.Contains("StatusToneConverter", StringComparison.Ordinal))
                .Select(v => $"{v.Where}: {v.Value}"),
        ];

        Assert.True(hits.Length == 0, "Foregrounds outside the asserted palette:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void TheBudgetStatusForegroundConverter_OnlyReturnsAssertedTextTokens()
    {
        // DashboardView binds a text Foreground through this converter, so its output set is part of the palette.
        string source = File.ReadAllText(Path.Combine(_uiRoot, "Converters", "BudgetStatusBrushConverter.cs"));
        string[] keys = [.. System.Text.RegularExpressions.Regex.Matches(source, "=> \"(?<key>\\w+Brush)\"").Select(m => m.Groups["key"].Value)];

        Assert.Equal(new[] { "WarningBrush", "ExpenseBrush", "IncomeBrush" }, keys);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void TheStatusChipConverter_OnlyReturnsAssertedTextOnWashPairs(string variant)
    {
        // DebtsView binds a chip's text and fill through StatusToneConverter, so every (text, fill) pair it can return must be
        // one the token pairs above already measure at 4.5:1, in each variant.
        Dictionary<string, Rgb> palette = Palette(variant);
        (string Background, string Foreground)[] returned =
        [
            .. new object?[]
            {
                ControlDeFinanzasIA.Domain.Debts.DebtPaymentStatus.Pending,
                ControlDeFinanzasIA.Domain.Debts.DebtPaymentStatus.Paid,
                ControlDeFinanzasIA.Domain.Debts.DebtPaymentStatus.Skipped,
                ControlDeFinanzasIA.Domain.Debts.DebtStatus.Active,
                ControlDeFinanzasIA.Domain.Debts.DebtStatus.Closed,
                null,
            }.Select(ControlDeFinanzasIA.UI.Converters.StatusToneConverter.ToneKeys),
        ];

        Assert.All(returned, pair =>
        {
            Assert.Contains(pair.Foreground, _textBrushes);
            double ratio = Ratio(palette[pair.Foreground.Replace("Brush", string.Empty, StringComparison.Ordinal)], palette[pair.Background.Replace("Brush", string.Empty, StringComparison.Ordinal)]);
            Assert.True(ratio >= _textMinimum, $"[{variant}] {pair.Foreground} on {pair.Background}: {ratio:0.00}:1.");
        });
    }

    [Fact]
    public void TheChatAndAnomalyConverters_OnlyReturnAssertedTokens()
    {
        string chat = File.ReadAllText(Path.Combine(_uiRoot, "Converters", "ChatMessageStateBrushConverter.cs"));
        string[] chatKeys = [.. System.Text.RegularExpressions.Regex.Matches(chat, "\"(?<key>\\w+Brush)\"").Select(m => m.Groups["key"].Value).Distinct()];
        Assert.All(chatKeys, key => Assert.Contains(key, _textBrushes));

        string anomaly = File.ReadAllText(Path.Combine(_uiRoot, "Converters", "AnomalySeverityBrushConverter.cs"));
        string[] anomalyKeys = [.. System.Text.RegularExpressions.Regex.Matches(anomaly, "\"(?<key>\\w+Brush)\"").Select(m => m.Groups["key"].Value).Distinct()];
        string[] allowed = ["AccentBrush", "WarningBrush", "ExpenseBrush", "AccentSoftBrush", "WarningSoftBrush", "ExpenseSoftBrush"];
        Assert.All(anomalyKeys, key => Assert.Contains(key, allowed));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void TheChartPalette_MatchesTheTokens_AndEverySliceIsReadableOnTheCard(string variant)
    {
        Dictionary<string, Rgb> palette = Palette(variant);
        ChartColors chart = ChartPalette.For(Enum.Parse<AppTheme>(variant));

        Assert.Equal(Hex(palette["Income"]), chart.IncomeHex, ignoreCase: true);
        Assert.Equal(Hex(palette["Expense"]), chart.ExpenseHex, ignoreCase: true);
        Assert.Equal(Hex(palette["TextSecondary"]), chart.AxisTextHex, ignoreCase: true);
        Assert.Equal(Hex(palette["Divider"]), chart.GridHex, ignoreCase: true);
        Assert.Equal(Hex(palette["Surface"]), chart.SurfaceHex, ignoreCase: true);
        Assert.True(chart.SliceHexes.Count >= 8);
        Assert.Equal(chart.SliceHexes.Count, chart.SliceHexes.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (string hex in chart.SliceHexes)
        {
            double ratio = Ratio(Rgb.Parse(hex), palette["Surface"]);
            Assert.True(ratio >= _graphicMinimum, $"[{variant}] Chart slice {hex} on Surface: {ratio:0.00}:1.");
        }

        Assert.True(Ratio(palette["TextSecondary"], palette["Surface"]) >= _textMinimum, "Axis and legend text sits on the card.");
    }

    [Fact]
    public void TheLightChartPalette_StaysTheOneTheStaticMembersExpose()
    {
        Assert.Equal(ChartPalette.For(AppTheme.Light).IncomeHex, ChartPalette.IncomeHex);
        Assert.Equal(ChartPalette.For(AppTheme.Light).SliceHexes, ChartPalette.SliceHexes);
        Assert.NotEqual(ChartPalette.For(AppTheme.Light).AxisTextHex, ChartPalette.For(AppTheme.Dark).AxisTextHex);
    }

    [Fact]
    public void TheMeasurement_CanFail_OnAKnownBadPair()
    {
        // Control case: a light grey on white really fails, so the assertions above can fail.
        Assert.True(Ratio(Rgb.Parse("#B0B4BE"), Rgb.Parse("#FFFFFF")) < _textMinimum);
        Assert.True(Ratio(Rgb.Parse("#14192B"), Rgb.Parse("#FFFFFF")) >= _textMinimum);
        Assert.True(Ratio(Rgb.Parse("#5A5F6E"), Rgb.Parse("#14130F")) < _textMinimum);
    }

    private static string Hex(Rgb c) => $"#{(int)Math.Round(c.R * 255):X2}{(int)Math.Round(c.G * 255):X2}{(int)Math.Round(c.B * 255):X2}";

    private static IEnumerable<(string Where, string Value)> ForegroundValues()
    {
        var files = new List<string>();
        foreach (string folder in new[] { "Views", "Controls", "Styles" })
        {
            files.AddRange(Directory.EnumerateFiles(Path.Combine(_uiRoot, folder), "*.axaml", SearchOption.AllDirectories));
        }

        foreach (string file in files)
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            string relative = Path.GetRelativePath(_uiRoot, file);
            foreach (XElement element in document.Descendants())
            {
                if (element.Name.LocalName == "Setter" && (string?)element.Attribute("Property") == "Foreground" && !IsProgressBarFill(element))
                {
                    yield return ($"{relative}:{((System.Xml.IXmlLineInfo)element).LineNumber}", (string?)element.Attribute("Value") ?? string.Empty);
                }

                XAttribute? attribute = element.Attribute("Foreground");
                if (attribute is not null && element.Name.LocalName != "ProgressBar")
                {
                    yield return ($"{relative}:{((System.Xml.IXmlLineInfo)attribute).LineNumber}", attribute.Value);
                }
            }
        }
    }

    /// <summary>A ProgressBar's Foreground is its fill (a graphic object asserted at 3:1 above), not text.</summary>
    private static bool IsProgressBarFill(XElement setter)
    {
        string? selector = (string?)setter.Parent?.Attribute("Selector");
        return selector is not null && selector.Contains("ProgressBar", StringComparison.Ordinal);
    }

    private static string StyleSetter(string selector, string property) =>
        StyleSetterOrNull(selector, property) ?? throw new InvalidOperationException($"No setter {property} on {selector}.");

    private static string? StyleSetterOrNull(string selector, string property)
    {
        var document = XDocument.Load(Path.Combine(_uiRoot, "Styles", "LedgerStyles.axaml"));
        return document.Descendants()
            .Where(e => e.Name.LocalName == "Style" && (string?)e.Attribute("Selector") == selector)
            .SelectMany(e => e.Elements())
            .Where(e => e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") == property)
            .Select(e => (string?)e.Attribute("Value"))
            .FirstOrDefault();
    }

    private static IEnumerable<string> ResourceKeys(string variant)
    {
        var document = XDocument.Load(Path.Combine(_uiRoot, "Styles", $"Tokens.{variant}.axaml"));
        return document.Root!.Elements().Select(e => (string?)e.Attribute(_xaml + "Key")).Where(k => k is not null).Select(k => k!);
    }

    private static Dictionary<string, Rgb> Palette(string variant)
    {
        var document = XDocument.Load(Path.Combine(_uiRoot, "Styles", $"Tokens.{variant}.axaml"));
        var palette = new Dictionary<string, Rgb>(StringComparer.Ordinal);
        foreach (XElement color in document.Descendants().Where(e => e.Name.LocalName == "Color"))
        {
            string key = (string?)color.Attribute(_xaml + "Key") ?? string.Empty;
            if (key.EndsWith("Color", StringComparison.Ordinal) && !key.StartsWith("SystemAccent", StringComparison.Ordinal))
            {
                palette[key[..^"Color".Length]] = Rgb.Parse(color.Value.Trim());
            }
        }

        // The wash colours are translucent overlays, not opaque palette entries.
        foreach (string overlay in new[] { "HoverWash", "PressedWash", "OnAccentWash" })
        {
            palette.Remove(overlay);
        }

        return palette;
    }

    private static double Ratio(Rgb foreground, Rgb background)
    {
        double a = foreground.Luminance;
        double b = background.Luminance;
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private readonly record struct Rgb(double R, double G, double B, double Alpha)
    {
        public double Luminance => (0.2126 * Linear(R)) + (0.7152 * Linear(G)) + (0.0722 * Linear(B));

        public static Rgb Parse(string hex)
        {
            string value = hex.TrimStart('#');
            double alpha = 1;
            if (value.Length == 8)
            {
                alpha = int.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
                value = value[2..];
            }

            return new Rgb(
                int.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
                int.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
                int.Parse(value[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0,
                alpha);
        }

        private static double Linear(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
