using System.Xml;
using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.Packaging;
using ControlDeFinanzasIA.Tests.Unit.UI;

namespace ControlDeFinanzasIA.Tests.Unit.Accessibility;

/// <summary>
/// F-021 S8 (R-17, R-19, DD-11 scans 1 and 3): the REAL <c>.axaml</c> files, parsed as XML. Every button whose content
/// is not plain text and every input without text content carries an <c>AutomationProperties.Name</c> (or
/// <c>LabeledBy</c>); a placeholder is NOT accepted as a name.
/// </summary>
public sealed class AccessibilityXamlScanTests
{
    private static readonly string[] _buttons = ["Button", "ToggleButton", "RepeatButton"];

    private static readonly string[] _inputs = ["TextBox", "ComboBox", "DatePicker", "NumericUpDown", "ToggleSwitch", "Slider", "CalendarDatePicker"];

    /// <summary>Pre-existing debt that is accepted on purpose, with the reason. Empty: S8 fixed every hit.</summary>
    private static readonly string[] _allowlist = [];

    [Fact]
    public void TheScan_CoversEveryViewAndTheMainWindow()
    {
        string[] files = HardcodedStringsScanTests.AllAxaml();

        Assert.Contains(Path.Combine("Views", "MainWindow.axaml"), files);
        Assert.Contains(Path.Combine("Views", "ErrorOverlayView.axaml"), files);
        Assert.Contains(Path.Combine("Controls", "EmptyState.axaml"), files);
        Assert.Contains(Path.Combine("Views", "Calendar", "MonthGrid.axaml"), files);
        Assert.True(files.Length >= 16, $"Only {files.Length} view files found.");
    }

    [Fact]
    public void EveryTextlessButton_SetsAnAutomationName()
    {
        string[] hits = [.. Scan((element, _) => IsTextlessButton(element) && !HasName(element))];

        Assert.True(hits.Length == 0, "Buttons without a text Content and without AutomationProperties.Name:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void EveryInputWithoutTextContent_SetsAnAutomationName_AndAPlaceholderIsNotAName()
    {
        string[] hits = [.. Scan((element, _) => IsUnlabelledInput(element))];

        Assert.True(hits.Length == 0, "Inputs without AutomationProperties.Name / LabeledBy:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void TheScanner_FlagsTheKnownBadShapes_AndAcceptsTheGoodOnes()
    {
        // Control case: proves the predicates can fail, so the green results above are meaningful.
        XElement Parse(string xml) => XElement.Parse("<Panel xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" + xml + "</Panel>").Elements().Single();

        Assert.True(IsTextlessButton(Parse("<Button Content=\"✕\"/>")));
        Assert.True(IsTextlessButton(Parse("<Button><StackPanel><TextBlock Text=\"x\"/></StackPanel></Button>")));
        Assert.False(IsTextlessButton(Parse("<Button Content=\"Save\"/>")));
        Assert.False(IsTextlessButton(Parse("<Button Content=\"{x:Static resources:Strings.Save}\"/>")));
        Assert.True(IsUnlabelledInput(Parse("<TextBox Watermark=\"Name\"/>")));
        Assert.True(IsUnlabelledInput(Parse("<ComboBox PlaceholderText=\"Pick\"/>")));
        Assert.False(IsUnlabelledInput(Parse("<TextBox Watermark=\"Name\" AutomationProperties.Name=\"Name\"/>")));
        Assert.False(IsUnlabelledInput(Parse("<CheckBox Content=\"{x:Static resources:Strings.X}\"/>")));
        Assert.True(IsUnlabelledInput(Parse("<CheckBox IsChecked=\"True\"/>")));
        Assert.True(HasName(Parse("<Button><AutomationProperties.Name><MultiBinding/></AutomationProperties.Name></Button>")));
        Assert.False(HasName(Parse("<Button><Button.Content><MultiBinding/></Button.Content></Button>")));
        Assert.True(IsTextlessButton(Parse("<Button><Button.Content><MultiBinding/></Button.Content></Button>")));
    }

    private static IEnumerable<string> Scan(Func<XElement, string, bool> predicate)
    {
        foreach (string relative in HardcodedStringsScanTests.AllAxaml())
        {
            string path = RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", relative);
            var document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement element in document.Descendants().Where(e => predicate(e, relative)))
            {
                string where = $"{relative}:{((IXmlLineInfo)element).LineNumber} <{element.Name.LocalName}>";
                if (!_allowlist.Contains(where, StringComparer.Ordinal))
                {
                    yield return where;
                }
            }
        }
    }

    /// <summary>
    /// A name is set either as an attribute or, when it needs a multi-binding, as the property element
    /// <c>&lt;AutomationProperties.Name&gt;...&lt;/AutomationProperties.Name&gt;</c> inside the control.
    /// </summary>
    private static bool HasName(XElement element) =>
        element.Attributes().Any(a => a.Name.LocalName is "AutomationProperties.Name" or "AutomationProperties.LabeledBy" && a.Value.Length > 0)
        || element.Elements().Any(e => e.Name.LocalName is "AutomationProperties.Name" or "AutomationProperties.LabeledBy");

    /// <summary>Text content means a Content attribute (resource, binding or a literal with a letter) or a text node.</summary>
    private static bool HasTextContent(XElement element)
    {
        string? content = (string?)element.Attributes().FirstOrDefault(a => a.Name.LocalName == "Content");
        if (content is not null)
        {
            return content.StartsWith('{') || content.Any(char.IsLetter);
        }

        return element.Nodes().OfType<XText>().Any(t => t.Value.Any(char.IsLetter));
    }

    private static bool IsTextlessButton(XElement element) =>
        _buttons.Contains(element.Name.LocalName, StringComparer.Ordinal) && !HasTextContent(element);

    private static bool IsUnlabelledInput(XElement element)
    {
        string name = element.Name.LocalName;
        if (name == "CheckBox" || name == "RadioButton")
        {
            return !HasTextContent(element) && !HasName(element);
        }

        return _inputs.Contains(name, StringComparer.Ordinal) && !HasName(element);
    }
}
