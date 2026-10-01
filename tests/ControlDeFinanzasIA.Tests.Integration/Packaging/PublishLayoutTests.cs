using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Packaging;

/// <summary>
/// F-021 S9 (R-21, S-43): runs <c>scripts/verify-layout.ps1</c> over the REAL publish output of each RID in
/// <c>artifacts/publish/</c>. A missing folder fails loudly (see <see cref="PackagingSupport.RequireArtifacts"/>);
/// the Category trait lets a local run filter these out with <c>--filter "Category!=Packaging"</c>.
/// </summary>
[Trait("Category", "Packaging")]
public sealed class PublishLayoutTests
{
    public static TheoryData<string> AllRids()
    {
        var data = new TheoryData<string>();
        foreach (string rid in PackagingSupport.Rids)
        {
            data.Add(rid);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllRids))]
    public void RealPublishOutputHasTheExecutableAndEveryNativeLibrary(string rid)
    {
        string folder = Path.Combine(PackagingSupport.Artifacts, "publish", rid);
        if (!PackagingSupport.RequireArtifacts(folder))
        {
            return;
        }

        (int exit, string output) = PackagingSupport.RunScript("verify-layout.ps1", "-Rid", rid);

        Assert.True(exit == 0, output);
        Assert.Contains("Layout OK for " + rid, output, StringComparison.Ordinal);
    }

    [Fact]
    public void LayoutCheckFailsAndNamesTheMissingFilesForAnIncompleteFolder()
    {
        string scratch = Path.Combine(Path.GetTempPath(), "cdfia-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            File.WriteAllBytes(Path.Combine(scratch, "ControlDeFinanzasIA.UI"), new byte[8192]);

            (int exit, string output) = PackagingSupport.RunScript("verify-layout.ps1", "-Rid", "linux-x64", "-PublishDir", scratch);

            Assert.NotEqual(0, exit);
            Assert.Contains("libSkiaSharp.so", output, StringComparison.Ordinal);
            Assert.Contains("libe_sqlite3.so", output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    [Fact]
    public void LayoutCheckRejectsANativeLibraryOfTheWrongPlatform()
    {
        string scratch = Path.Combine(Path.GetTempPath(), "cdfia-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            // A Windows PE file name-matched as a Linux library: existence alone would pass, the magic bytes must not.
            byte[] pe = new byte[8192];
            pe[0] = (byte)'M';
            pe[1] = (byte)'Z';
            foreach (string name in new[]
            {
                "ControlDeFinanzasIA.UI", "libSkiaSharp.so", "libHarfBuzzSharp.so", "libe_sqlite3.so",
                "libQuestPdfSkia.so", "libqpdf.so", "libcoreclr.so", "libhostfxr.so",
            })
            {
                File.WriteAllBytes(Path.Combine(scratch, name), pe);
            }

            Directory.CreateDirectory(Path.Combine(scratch, "es-DO"));
            File.WriteAllBytes(Path.Combine(scratch, "es-DO", "ControlDeFinanzasIA.UI.resources.dll"), pe);

            (int exit, string output) = PackagingSupport.RunScript("verify-layout.ps1", "-Rid", "linux-x64", "-PublishDir", scratch);

            Assert.NotEqual(0, exit);
            Assert.Contains("wrong platform", output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }
}
