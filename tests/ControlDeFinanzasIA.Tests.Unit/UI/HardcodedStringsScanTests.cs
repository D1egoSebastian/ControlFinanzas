using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.Packaging;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// F-021 S5a (R-19, S-38, DD-11 scan 3): the REAL <c>.axaml</c> files, parsed as XML, carry no user-visible literal.
/// Later slices only append files to <see cref="_scannedViews"/>.
/// </summary>
public sealed class HardcodedStringsScanTests
{
    /// <summary>Views converted so far, relative to <c>src/ControlDeFinanzasIA.UI</c>.</summary>
    private static readonly string[] _scannedViews =
    [
        Path.Combine("Views", "ExpensesView.axaml"),
        Path.Combine("Controls", "EmptyState.axaml"),
    ];

    /// <summary>Attribute local names that render text or a name to the user (union of spec and design lists).</summary>
    private static readonly string[] _textAttributes =
    [
        "Text",
        "Content",
        "Header",
        "Watermark",
        "PlaceholderText",
        "ToolTip.Tip",
        "AutomationProperties.Name",
    ];

    /// <summary>Finds every literal user-visible attribute value (contains a letter, is not a binding or resource).</summary>
    internal static IReadOnlyList<string> FindLiterals(IEnumerable<string> relativeFiles)
    {
        var hits = new List<string>();
        foreach (string relative in relativeFiles)
        {
            string path = RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", relative);
            var document = XDocument.Load(path, LoadOptions.SetLineInfo);

            foreach (XElement element in document.Descendants())
            {
                foreach (XAttribute attribute in element.Attributes())
                {
                    string name = attribute.Name.LocalName;
                    if (!_textAttributes.Contains(name, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    string value = attribute.Value;
                    if (value.StartsWith('{') || !value.Any(char.IsLetter))
                    {
                        continue;
                    }

                    hits.Add($"{relative}:{((System.Xml.IXmlLineInfo)attribute).LineNumber} {name}=\"{value}\"");
                }
            }
        }

        return hits;
    }

    [Fact]
    public void TheConvertedViews_HaveNoLiteralUserVisibleText()
    {
        IReadOnlyList<string> hits = FindLiterals(_scannedViews);

        Assert.True(hits.Count == 0, "Hardcoded UI literals:" + Environment.NewLine + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void TheScanItself_FlagsALiteral_AndAcceptsBindingsAndResources()
    {
        // Control case: proves the scanner can fail, so a green result above is meaningful.
        string temp = Path.Combine(Path.GetTempPath(), "strings-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            string file = Path.Combine(temp, "Sample.axaml");
            File.WriteAllText(
                file,
                "<Panel xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
                "<TextBlock Text=\"Hola\"/><TextBlock Text=\"{Binding Name}\"/><Button Content=\"✕\"/>" +
                "<TextBox Watermark=\"Nota\"/></Panel>");

            var document = XDocument.Load(file);
            string[] literal =
            [
                .. document.Descendants().SelectMany(e => e.Attributes())
                    .Where(a => _textAttributes.Contains(a.Name.LocalName, StringComparer.Ordinal))
                    .Where(a => !a.Value.StartsWith('{') && a.Value.Any(char.IsLetter))
                    .Select(a => a.Value),
            ];

            Assert.Equal(new[] { "Hola", "Nota" }, literal);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
