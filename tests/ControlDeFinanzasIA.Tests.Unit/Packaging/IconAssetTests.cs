using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ControlDeFinanzasIA.Tests.Unit.UI;
using ControlDeFinanzasIA.UI.Views;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>F-021 S1: the icon set exists, is well-formed and is wired to the window (R-2, S-3, DD-12).</summary>
[Collection(HeadlessUi.CollectionName)]
public sealed class IconAssetTests
{
    private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static string IconPath(string name) =>
        RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Assets", "Icon", name);

    [Fact]
    public void AllFourIconSourcesExist()
    {
        foreach (string name in new[] { "app-icon.svg", "app-icon.png", "app-icon.ico", "app-icon.icns" })
        {
            Assert.True(File.Exists(IconPath(name)), name);
            Assert.True(new FileInfo(IconPath(name)).Length > 100, name);
        }
    }

    [Fact]
    public void SvgUsesOnlyTheRendererSubset()
    {
        var doc = XDocument.Load(IconPath("app-icon.svg"));
        var elements = doc.Root!.Descendants().Select(e => e.Name.LocalName).Distinct().ToList();

        Assert.Equal("svg", doc.Root.Name.LocalName);
        Assert.All(elements, n => Assert.True(n is "rect" or "path" or "g" or "title", n));
        Assert.All(
            doc.Root.Descendants().Where(e => e.Name.LocalName == "path"),
            p => Assert.True(p.Attribute("d") != null && p.Attribute("fill") != null));
        Assert.True(doc.Root.Descendants().Count(e => e.Name.LocalName is "rect" or "path") >= 5);
    }

    [Fact]
    public void ContractPngIs512Square() => Assert.Equal((512, 512), PngSize(IconPath("app-icon.png")));

    [Fact]
    public void MasterPngIs1024Square() => Assert.Equal((1024, 1024), PngSize(IconPath("app-icon-1024.png")));

    [Fact]
    public void IcoHeaderDeclaresTheSixExpectedSizes()
    {
        byte[] ico = File.ReadAllBytes(IconPath("app-icon.ico"));

        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(0, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2, 2)));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4, 2));
        Assert.Equal(6, count);

        var sizes = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int entry = 6 + (i * 16);
            sizes.Add(ico[entry] == 0 ? 256 : ico[entry]);
            uint len = BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(entry + 8, 4));
            uint off = BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(entry + 12, 4));
            Assert.True(off + len <= (uint)ico.Length);
            Assert.Equal(_pngSignature, ico.AsSpan((int)off, 8).ToArray());
        }

        Assert.Equal(new[] { 16, 24, 32, 48, 64, 256 }, sizes.Order().ToArray());
    }

    [Fact]
    public void IcnsHasMagicExactLengthAndRetinaChunks()
    {
        byte[] icns = File.ReadAllBytes(IconPath("app-icon.icns"));

        Assert.Equal("icns", Encoding.ASCII.GetString(icns, 0, 4));
        Assert.Equal((uint)icns.Length, BinaryPrimitives.ReadUInt32BigEndian(icns.AsSpan(4, 4)));

        var types = new List<string>();
        int pos = 8;
        while (pos < icns.Length)
        {
            types.Add(Encoding.ASCII.GetString(icns, pos, 4));
            uint len = BinaryPrimitives.ReadUInt32BigEndian(icns.AsSpan(pos + 4, 4));
            Assert.True(len > 8u);
            pos += (int)len;
        }

        Assert.Equal(icns.Length, pos);
        foreach (string chunk in new[] { "ic07", "ic08", "ic09", "ic10" })
        {
            Assert.Contains(chunk, types);
        }
    }

    [Fact]
    public void CsprojSetsApplicationIconAndAvaloniaResource()
    {
        var csproj = XDocument.Load(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "ControlDeFinanzasIA.UI.csproj"));

        Assert.Equal(
            @"Assets\Icon\app-icon.ico",
            csproj.Descendants().Single(e => e.Name.LocalName == "ApplicationIcon").Value);
        Assert.Contains(
            csproj.Descendants().Where(e => e.Name.LocalName == "AvaloniaResource").Select(e => (string?)e.Attribute("Include")),
            i => i != null && Regex.IsMatch(i, @"Assets\\Icon\\\*\.ico;.*\*\.png"));
    }

    [Fact]
    public async Task RealMainWindowLoadsItsIcon()
    {
        bool hasIcon = await HeadlessUi.OnUiThread(() =>
        {
            var window = new MainWindow();
            try
            {
                return Task.FromResult(window.Icon is not null);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(hasIcon);
    }

    private static (int W, int H) PngSize(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        Assert.Equal(_pngSignature, b.AsSpan(0, 8).ToArray());
        Assert.Equal("IHDR", Encoding.ASCII.GetString(b, 12, 4));
        return ((int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16, 4)), (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20, 4)));
    }
}
