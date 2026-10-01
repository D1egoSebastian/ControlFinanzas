using System.Globalization;
using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.Packaging;
using ControlDeFinanzasIA.UI.Views.Charts;

namespace ControlDeFinanzasIA.Tests.Unit.Accessibility;

/// <summary>
/// WCAG 2.x contrast of the REAL palette (<c>Tokens.axaml</c> parsed) and of the pairs the REAL styles and views use.
/// Thresholds: 4.5:1 for text, 3:1 for focus rings, input boundaries and graphical fills. No failing pair is excluded:
/// a failure is fixed in the tokens or the style, not in this list. Pre-1.0 slice E replaced the dark "Libro Mayor"
/// palette with the light "Sand and Cobalt" palette; the thresholds did not move.
/// </summary>
public sealed class ContrastTests
{
    private const double _textMinimum = 4.5;
    private const double _graphicMinimum = 3.0;

    private static readonly string _uiRoot = RepoRoot.Combine("src", "ControlDeFinanzasIA.UI");

    private static readonly XNamespace _xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Foreground resources a view or style may use for text; every one has its pairs asserted below.</summary>
    private static readonly string[] _textBrushes =
    [
        "TextBrush", "TextSecondaryBrush", "TextMutedBrush", "AccentBrush", "OnAccentBrush", "IncomeBrush", "ExpenseBrush", "WarningBrush",
    ];

    /// <summary>Surfaces that text can sit on.</summary>
    private static readonly string[] _surfaces = ["Canvas", "Surface", "SurfaceMuted"];

    public static TheoryData<string, string, string, double> TokenPairs()
    {
        var data = new TheoryData<string, string, string, double>();
        foreach (string foreground in new[] { "Text", "TextSecondary", "TextMuted", "Accent", "Income", "Expense", "Warning" })
        {
            foreach (string background in _surfaces)
            {
                data.Add($"{foreground} text on {background}", foreground, background, _textMinimum);
            }
        }

        // Soft semantic washes (chips, notices, selected sidebar item) carry their own text colour.
        data.Add("Text on AccentSoft", "Text", "AccentSoft", _textMinimum);
        data.Add("Accent text on AccentSoft", "Accent", "AccentSoft", _textMinimum);
        data.Add("Income text on IncomeSoft", "Income", "IncomeSoft", _textMinimum);
        data.Add("Expense text on ExpenseSoft", "Expense", "ExpenseSoft", _textMinimum);
        data.Add("Warning text on WarningSoft", "Warning", "WarningSoft", _textMinimum);
        data.Add("TextSecondary on AccentSoft", "TextSecondary", "AccentSoft", _textMinimum);

        // Filled buttons: the label on the Accent fill and on its hover fill.
        data.Add("OnAccent label on the Accent button fill", "OnAccent", "Accent", _textMinimum);
        data.Add("OnAccent label on the AccentHover button fill", "OnAccent", "AccentHover", _textMinimum);
        data.Add("OnAccent label on the AccentPressed button fill", "OnAccent", "AccentPressed", _textMinimum);
        data.Add("OnAccent count on the Expense notification badge", "OnAccent", "Expense", _textMinimum);

        // Focus ring (3:1) against every surface it can touch, including the primary fill it is drawn over.
        foreach (string background in new[] { "Canvas", "Surface", "SurfaceMuted", "Accent", "AccentSoft" })
        {
            data.Add($"Focus ring on {background}", "FocusRing", background, _graphicMinimum);
        }

        // Input boundaries and graphical objects (3:1).
        data.Add("Input border on Surface", "InputBorder", "Surface", _graphicMinimum);
        data.Add("Input border on Canvas", "InputBorder", "Canvas", _graphicMinimum);
        data.Add("Accent focus border on Surface", "Accent", "Surface", _graphicMinimum);
        data.Add("Income progress/amount fill on Surface", "Income", "Surface", _graphicMinimum);
        data.Add("Expense progress/amount fill on Surface", "Expense", "Surface", _graphicMinimum);
        data.Add("Warning progress fill on Surface", "WarningFill", "Surface", _graphicMinimum);
        return data;
    }

    [Theory]
    [MemberData(nameof(TokenPairs))]
    public void TheTokenPair_MeetsItsThreshold(string pair, string foreground, string background, double minimum)
    {
        Dictionary<string, Rgb> palette = Palette();

        double ratio = Ratio(palette[foreground], palette[background]);

        Assert.True(ratio >= minimum, $"{pair}: {ratio:0.00}:1 is below {minimum}:1.");
    }

    [Fact]
    public void TheNewPalette_IsLight_AndTheCanvasIsNotPlainWhite()
    {
        Dictionary<string, Rgb> palette = Palette();

        Assert.True(palette["Canvas"].Luminance > 0.8, "The canvas is a light surface.");
        Assert.True(palette["Surface"].Luminance > palette["Canvas"].Luminance, "Cards sit above the canvas (lighter).");
        Assert.True(palette["Text"].Luminance < 0.05, "Body text is near-ink.");
    }

    [Fact]
    public void TheDangerLabelAndTheExpenseAmount_UseTheExpenseTextToken()
    {
        string styles = File.ReadAllText(Path.Combine(_uiRoot, "Styles", "LedgerStyles.axaml"));

        Assert.Equal("{StaticResource ExpenseBrush}", StyleSetter("Button.ledger-btn-danger", "Foreground"));
        Assert.Equal("{StaticResource ExpenseBrush}", StyleSetter("TextBlock.ledger-amount.expense", "Foreground"));
        Assert.DoesNotContain("TerracottaBrush", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAdjacentMonthCalendarDays_StayReadable_WithoutOpacity()
    {
        Dictionary<string, Rgb> palette = Palette();
        string? opacity = StyleSetterOrNull("Button.calendar-day.adjacent", "Opacity");

        Assert.Null(opacity);
        Assert.Equal("{StaticResource TextMutedBrush}", StyleSetter("Button.calendar-day.adjacent", "Foreground"));
        Assert.True(Ratio(palette["TextMuted"], palette["Surface"]) >= _textMinimum);
    }

    [Fact]
    public void TheSidebarPairs_MeetTextContrast_IncludingTheDigitHintAndTheCurrentWash()
    {
        Dictionary<string, Rgb> palette = Palette();

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
            Assert.True(ratio >= _textMinimum, $"{name}: {ratio:0.00}:1.");
        }

        Assert.Equal("{StaticResource TextMutedBrush}", StyleSetter("TextBlock.sidebar-hint", "Foreground"));
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
                .Where(v => !_textBrushes.Any(b => v.Value == "{StaticResource " + b + "}"))
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

    [Fact]
    public void TheStatusChipConverter_OnlyReturnsAssertedTextOnWashPairs()
    {
        // DebtsView binds a chip's text and fill through StatusToneConverter, so every (text, fill) pair it can return must be
        // one the token pairs above already measure at 4.5:1.
        Dictionary<string, Rgb> palette = Palette();
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
            Assert.True(ratio >= _textMinimum, $"{pair.Foreground} on {pair.Background}: {ratio:0.00}:1.");
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

    [Fact]
    public void TheChartPalette_MatchesTheTokens_AndEverySliceIsReadableOnTheCard()
    {
        Dictionary<string, Rgb> palette = Palette();

        Assert.Equal(Hex(palette["Income"]), ChartPalette.IncomeHex, ignoreCase: true);
        Assert.Equal(Hex(palette["Expense"]), ChartPalette.ExpenseHex, ignoreCase: true);
        Assert.Equal(Hex(palette["TextSecondary"]), ChartPalette.AxisTextHex, ignoreCase: true);
        Assert.True(ChartPalette.SliceHexes.Count >= 8);
        Assert.Equal(ChartPalette.SliceHexes.Count, ChartPalette.SliceHexes.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (string hex in ChartPalette.SliceHexes)
        {
            double ratio = Ratio(Rgb.Parse(hex), palette["Surface"]);
            Assert.True(ratio >= _graphicMinimum, $"Chart slice {hex} on Surface: {ratio:0.00}:1.");
        }
    }

    [Fact]
    public void TheMeasurement_CanFail_OnAKnownBadPair()
    {
        // Control case: a light grey on white really fails, so the assertions above can fail.
        Assert.True(Ratio(Rgb.Parse("#B0B4BE"), Rgb.Parse("#FFFFFF")) < _textMinimum);
        Assert.True(Ratio(Rgb.Parse("#14192B"), Rgb.Parse("#FFFFFF")) >= _textMinimum);
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

    private static Dictionary<string, Rgb> Palette()
    {
        var document = XDocument.Load(Path.Combine(_uiRoot, "Styles", "Tokens.axaml"));
        var palette = new Dictionary<string, Rgb>(StringComparer.Ordinal);
        foreach (XElement color in document.Descendants().Where(e => e.Name.LocalName == "Color"))
        {
            string key = (string?)color.Attribute(_xaml + "Key") ?? string.Empty;
            if (key.EndsWith("Color", StringComparison.Ordinal))
            {
                palette[key[..^"Color".Length]] = Rgb.Parse(color.Value.Trim());
            }
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
