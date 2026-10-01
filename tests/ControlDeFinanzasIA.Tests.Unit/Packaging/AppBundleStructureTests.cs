using System.Xml.Linq;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>
/// F-021 S9: <c>scripts/package-macos-app.ps1</c> builds a structurally valid <c>.app</c> folder (R-23, S-45).
/// The script runs for real over a synthetic publish folder; only folder layout and plist content are checked,
/// the bundle is never launched here (no macOS on this machine).
/// </summary>
public sealed class AppBundleStructureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cdfia-appbundle-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string ReadPropsVersion()
    {
        var doc = XDocument.Load(RepoRoot.Combine("Directory.Build.props"));
        return doc.Descendants("Version").Single().Value.Trim();
    }

    private string BuildBundle()
    {
        string publish = Path.Combine(_root, "publish");
        string output = Path.Combine(_root, "out");
        Directory.CreateDirectory(Path.Combine(publish, "runtimes"));
        File.WriteAllBytes(Path.Combine(publish, "ControlDeFinanzasIA.UI"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(publish, "ControlDeFinanzasIA.UI.dll"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(publish, "libSkiaSharp.dylib"), [7, 8, 9]);
        File.WriteAllBytes(Path.Combine(publish, "runtimes", "extra.bin"), [10]);

        string script = RepoRoot.Combine("scripts", "package-macos-app.ps1");
        (int exit, string log) = PowerShellRunner.Run(script, "-PublishDir", publish, "-OutputDir", output);

        Assert.True(exit == 0, "package-macos-app.ps1 failed: " + log);
        return Path.Combine(output, "ControlDeFinanzasIA.app");
    }

    [Fact]
    public void BundleHasExecutableIconAndPublishFilesInTheExpectedPlaces()
    {
        string app = BuildBundle();

        Assert.True(File.Exists(Path.Combine(app, "Contents", "MacOS", "ControlDeFinanzasIA.UI")));
        Assert.True(File.Exists(Path.Combine(app, "Contents", "MacOS", "ControlDeFinanzasIA.UI.dll")));
        Assert.True(File.Exists(Path.Combine(app, "Contents", "MacOS", "libSkiaSharp.dylib")));
        Assert.True(File.Exists(Path.Combine(app, "Contents", "MacOS", "runtimes", "extra.bin")));

        string icns = Path.Combine(app, "Contents", "Resources", "AppIcon.icns");
        Assert.True(File.Exists(icns));
        Assert.Equal(
            File.ReadAllBytes(RepoRoot.Combine("src", "ControlDeFinanzasIA.UI", "Assets", "Icon", "app-icon.icns")),
            File.ReadAllBytes(icns));
    }

    [Fact]
    public void InfoPlistParsesAsAnXmlPlistWithTheRequiredKeys()
    {
        string app = BuildBundle();
        string plistPath = Path.Combine(app, "Contents", "Info.plist");

        var doc = XDocument.Load(plistPath);
        Assert.Equal("plist", doc.Root!.Name.LocalName);
        XElement dict = Assert.Single(doc.Root.Elements("dict"));
        var entries = new Dictionary<string, string>();
        XElement[] children = dict.Elements().ToArray();
        for (int i = 0; i < children.Length; i += 2)
        {
            Assert.Equal("key", children[i].Name.LocalName);
            entries[children[i].Value] = children[i + 1].Name.LocalName is "true" or "false"
                ? children[i + 1].Name.LocalName
                : children[i + 1].Value;
        }

        string version = ReadPropsVersion();
        Assert.Equal("ControlDeFinanzasIA.UI", entries["CFBundleExecutable"]);
        Assert.Equal("com.diego.controldefinanzasia", entries["CFBundleIdentifier"]);
        Assert.Equal(version, entries["CFBundleShortVersionString"]);
        Assert.Equal(version, entries["CFBundleVersion"]);
        Assert.Equal("AppIcon", entries["CFBundleIconFile"]);
        Assert.Equal("true", entries["NSHighResolutionCapable"]);
        Assert.Equal("12.0", entries["LSMinimumSystemVersion"]);
        Assert.Equal("APPL", entries["CFBundlePackageType"]);
        Assert.DoesNotContain("@@", File.ReadAllText(plistPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptFailsLoudlyWhenThePublishFolderIsMissing()
    {
        string script = RepoRoot.Combine("scripts", "package-macos-app.ps1");

        (int exit, string log) = PowerShellRunner.Run(
            script, "-PublishDir", Path.Combine(_root, "nope"), "-OutputDir", Path.Combine(_root, "out"));

        Assert.NotEqual(0, exit);
        Assert.Contains("publish", log, StringComparison.OrdinalIgnoreCase);
    }
}
