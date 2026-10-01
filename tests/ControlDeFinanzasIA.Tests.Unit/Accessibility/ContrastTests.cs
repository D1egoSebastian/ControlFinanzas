using System.Globalization;
using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.Packaging;

namespace ControlDeFinanzasIA.Tests.Unit.Accessibility;

/// <summary>
/// F-021 S8 (R-18, S-37, T12): WCAG 2.x contrast of the REAL palette (<c>Tokens.axaml</c> parsed) and of the pairs the
/// REAL styles and views use. Thresholds: 4.5:1 for text, 3:1 for focus rings and graphical fills. No failing pair is
/// excluded: a failure is fixed in the tokens or the style, not in this list.
/// </summary>
public sealed class ContrastTests
{
    private const double _textMinimum = 4.5;
    private const double _graphicMinimum = 3.0;

    private static readonly string _uiRoot = RepoRoot.Combine("src", "ControlDeFinanzasIA.UI");

    private static readonly XNamespace _xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Foreground resources a view or style may use for text; every one has its pairs asserted below.</summary>
    private static readonly string[] _textBrushes = ["PaperBrush", "PaperDimBrush", "AmberBrush", "SageBrush", "TerracottaTextBrush", "InkBrush"];

    public static TheoryData<string, string, string, double> TokenPairs()
    {
        var data = new TheoryData<string, string, string, double>();
        foreach (string foreground in new[] { "Paper", "PaperDim", "Amber", "Sage", "TerracottaText" })
        {
            foreach (string background in new[] { "Ink", "Surface" })
            {
                data.Add($"{foreground} text on {background}", foreground, background, _textMinimum);
            }
        }

        // Filled buttons: Ink label on the Amber (Expenses register) and Sage (Income register) fills.
        data.Add("Ink label on the Amber button fill", "Ink", "Amber", _textMinimum);
        data.Add("Ink label on the Sage button fill", "Ink", "Sage", _textMinimum);

        // Focus ring and graphical objects (3:1).
        data.Add("Amber focus ring on Ink", "Amber", "Ink", _graphicMinimum);
        data.Add("Amber focus ring on Surface", "Amber", "Surface", _graphicMinimum);
        data.Add("Terracotta fill/rule on Ink", "Terracotta", "Ink", _graphicMinimum);
        data.Add("Terracotta fill/rule on Surface (4.43:1, fills and rules only)", "Terracotta", "Surface", _graphicMinimum);
        data.Add("Sage progress fill on Surface", "Sage", "Surface", _graphicMinimum);
        data.Add("Amber progress fill on Surface", "Amber", "Surface", _graphicMinimum);
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
    public void TheDangerButtonHover_MeetsTextContrast_OnBothBackgrounds()
    {
        Dictionary<string, Rgb> palette = Palette();
        var hover = Rgb.Parse(StyleSetter("Button.ledger-btn-danger:pointerover", "Foreground"));

        foreach (string background in new[] { "Ink", "Surface" })
        {
            double ratio = Ratio(hover, palette[background]);
            Assert.True(ratio >= _textMinimum, $"Danger hover on {background}: {ratio:0.00}:1.");
        }
    }

    [Fact]
    public void TheDangerLabelAndTheExpenseAmount_UseTheTerracottaTextToken_NotTheFillToken()
    {
        Assert.Equal("{StaticResource TerracottaTextBrush}", StyleSetter("Button.ledger-btn-danger", "Foreground"));
        Assert.Equal("{StaticResource TerracottaTextBrush}", StyleSetter("TextBlock.ledger-amount.expense", "Foreground"));
    }

    [Fact]
    public void TheAdjacentMonthCalendarDays_StayReadable_AfterTheirOpacity()
    {
        Dictionary<string, Rgb> palette = Palette();
        string opacity = StyleSetterOrNull("Button.calendar-day.adjacent", "Opacity") ?? "1";
        double alpha = double.Parse(opacity, CultureInfo.InvariantCulture);

        Rgb effective = palette["PaperDim"].Over(palette["Ink"], alpha);

        double ratio = Ratio(effective, palette["Ink"]);
        Assert.True(ratio >= _textMinimum, $"Adjacent-month day number: {ratio:0.00}:1 at opacity {alpha}.");
    }

    [Fact]
    public void TheSidebarPairs_MeetTextContrast_IncludingTheDigitHintAndTheCurrentWash()
    {
        Dictionary<string, Rgb> palette = Palette();
        Rgb washedSurface = palette["AmberWash"].Over(palette["Surface"]);

        (string Name, double Ratio)[] pairs =
        [
            ("digit hint (PaperDim) on Surface", Ratio(palette["PaperDim"], palette["Surface"])),
            ("inactive label (PaperDim) on Surface", Ratio(palette["PaperDim"], palette["Surface"])),
            ("current label (Paper) on the amber wash", Ratio(palette["Paper"], washedSurface)),
            ("current digit hint (Amber) on the amber wash", Ratio(palette["Amber"], washedSurface)),
            ("hover label (Paper) on Surface", Ratio(palette["Paper"], palette["Surface"])),
        ];

        foreach ((string name, double ratio) in pairs)
        {
            Assert.True(ratio >= _textMinimum, $"{name}: {ratio:0.00}:1.");
        }

        Assert.Equal("{StaticResource PaperDimBrush}", StyleSetter("TextBlock.sidebar-hint", "Foreground"));
        Assert.Null(StyleSetterOrNull("TextBlock.sidebar-hint", "Opacity"));
    }

    [Fact]
    public void NoTextForeground_BindsToTheTerracottaFillToken()
    {
        string[] hits = [.. ForegroundValues().Where(v => v.Value.Contains("TerracottaBrush", StringComparison.Ordinal)).Select(v => $"{v.Where}: {v.Value}")];

        Assert.True(hits.Length == 0, "Terracotta (4.43:1 on Surface) is reserved for fills and rules; use TerracottaTextBrush for text:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void EveryTextForeground_IsOneOfTheTokensWhosePairsAreAssertedAbove()
    {
        string[] hits =
        [
            .. ForegroundValues()
                .Where(v => !_textBrushes.Any(b => v.Value == "{StaticResource " + b + "}"))
                .Where(v => v.Value != "#F5A9A3")
                .Where(v => !v.Value.Contains("BudgetStatusBrushConverter", StringComparison.Ordinal))
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

        Assert.Equal(new[] { "AmberBrush", "TerracottaTextBrush", "SageBrush" }, keys);
    }

    [Fact]
    public void TheMeasurement_CanFail_OnTheKnownBadPair()
    {
        // Control case: the Terracotta fill token on Surface is a real, measured failure for text.
        Dictionary<string, Rgb> palette = Palette();

        Assert.True(Ratio(palette["Terracotta"], palette["Surface"]) < _textMinimum);
        Assert.True(Ratio(palette["TerracottaText"], palette["Surface"]) >= _textMinimum);
    }

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
                if (attribute is not null)
                {
                    yield return ($"{relative}:{((System.Xml.IXmlLineInfo)attribute).LineNumber}", attribute.Value);
                }
            }
        }
    }

    /// <summary>A ProgressBar's Foreground is its fill (a graphic object asserted at 3:1 above), not text.</summary>
    private static bool IsProgressBarFill(XElement setter) =>
        ((string?)setter.Parent?.Attribute("Selector"))?.StartsWith("ProgressBar", StringComparison.Ordinal) == true;

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

        /// <summary>Composites this colour (its alpha times <paramref name="opacity"/>) over an opaque background.</summary>
        /// <param name="background">The opaque background.</param>
        /// <param name="opacity">Extra element opacity.</param>
        public Rgb Over(Rgb background, double opacity = 1)
        {
            double a = Alpha * opacity;
            return new Rgb((R * a) + (background.R * (1 - a)), (G * a) + (background.G * (1 - a)), (B * a) + (background.B * (1 - a)), 1);
        }

        private static double Linear(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
