using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Infrastructure.Import;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>Hostile-archive tests for <see cref="ClosedXmlTableReader"/> (F-020, R-7, S-7, C-4).</summary>
public sealed class ClosedXmlZipGuardTests
{
    private static readonly TabularReadOptions _options = new((char?)null);

    /// <summary>Builds a reader whose ClosedXML hand-off is observable: the flag flips when the workbook is opened.</summary>
    private static (ClosedXmlTableReader Reader, Func<bool> Opened) SpyReader()
    {
        bool opened = false;
        return (new ClosedXmlTableReader(() => opened = true), () => opened);
    }

    private static byte[] ZipWithZeros(long uncompressedBytes)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            ZipArchiveEntry entry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize);
            using Stream s = entry.Open();
            byte[] chunk = new byte[1024 * 1024];
            for (long written = 0; written < uncompressedBytes; written += chunk.Length)
            {
                int count = (int)Math.Min(chunk.Length, uncompressedBytes - written);
                s.Write(chunk, 0, count);
            }
        }

        return ms.ToArray();
    }

    /// <summary>Overwrites the declared uncompressed size in the central directory and the local header.</summary>
    private static void LieAboutSize(byte[] zip, uint declared)
    {
        for (int i = 0; i < zip.Length - 30; i++)
        {
            if (zip[i] == 0x50 && zip[i + 1] == 0x4B && zip[i + 2] == 0x01 && zip[i + 3] == 0x02)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(i + 24), declared);
            }
            else if (zip[i] == 0x50 && zip[i + 1] == 0x4B && zip[i + 2] == 0x03 && zip[i + 3] == 0x04)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(i + 22), declared);
            }
        }
    }

    private static void AssertFailure(Result result, string code, string reason)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error!.Code);
        Assert.Equal(reason, result.Error.Details![ImportErrorCodes.DetailReason][0]);
    }

    [Fact]
    public async Task ZipBomb_DeclaringItsRealSize_IsZipTooLargeAndClosedXmlNeverOpens()
    {
        byte[] bomb = ZipWithZeros(ImportLimits.MaxXlsxUncompressedBytes + 1024);
        Assert.True(bomb.Length < ImportLimits.MaxFileBytes, "The bomb must fit under the file-size limit.");
        (ClosedXmlTableReader reader, Func<bool> opened) = SpyReader();

        Result result = await reader.ReadAsync(bomb, _options);

        AssertFailure(result, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonZipTooLarge);
        Assert.False(opened());
    }

    [Fact]
    public async Task ZipBomb_LyingAboutItsSize_IsRejectedBeforeClosedXml()
    {
        byte[] bomb = ZipWithZeros(ImportLimits.MaxXlsxUncompressedBytes + 1024);
        LieAboutSize(bomb, 100);
        using (var check = new ZipArchive(new MemoryStream(bomb), ZipArchiveMode.Read))
        {
            Assert.Equal(100, check.Entries[0].Length);
        }

        (ClosedXmlTableReader reader, Func<bool> _) = SpyReader();
        var clock = Stopwatch.StartNew();

        Result result = await reader.ReadAsync(bomb, _options);

        // Measured behaviour: the .NET inflater hands out at most the DECLARED size, so a lying header cannot inflate
        // anything past it. The guard reads only 100 bytes; ClosedXML then sees a truncated part and fails cleanly.
        AssertFailure(result, ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonNotAWorkbook);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), "Guard took " + clock.Elapsed);
    }

    [Fact]
    public async Task TooManyEntries_IsTooLarge()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            for (int i = 0; i <= ImportLimits.MaxXlsxEntries; i++)
            {
                _ = zip.CreateEntry("e" + i + ".xml");
            }
        }

        (ClosedXmlTableReader reader, Func<bool> opened) = SpyReader();

        Result result = await reader.ReadAsync(ms.ToArray(), _options);

        AssertFailure(result, ImportErrorCodes.TooLarge, ImportErrorCodes.ReasonZipTooLarge);
        Assert.False(opened());
    }

    [Fact]
    public async Task ExactlyMaxEntries_PassesTheGuard()
    {
        byte[] baseline = await XlsxFixtures.ExpenseListAsync();
        using var source = new ZipArchive(new MemoryStream(baseline), ZipArchiveMode.Read);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (ZipArchiveEntry entry in source.Entries)
            {
                ZipArchiveEntry copy = zip.CreateEntry(entry.FullName);
                using Stream from = entry.Open();
                using Stream to = copy.Open();
                from.CopyTo(to);
            }

            for (int i = source.Entries.Count; i < ImportLimits.MaxXlsxEntries; i++)
            {
                _ = zip.CreateEntry("extra/e" + i + ".bin");
            }
        }

        Result result = await new ClosedXmlTableReader().ReadAsync(ms.ToArray(), _options);

        Assert.True(result.IsSuccess, result.Error?.Code);
    }

    [Fact]
    public async Task ZipThatIsNotAWorkbook_IsNotAWorkbook()
    {
        byte[] zip = XlsxHandBuilt.Package(("readme.txt", "hello"), ("notes/a.txt", "world"));

        Result result = await new ClosedXmlTableReader().ReadAsync(zip, _options);

        AssertFailure(result, ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonNotAWorkbook);
    }

    [Fact]
    public async Task TruncatedZip_IsNotAWorkbookAndNeverThrows()
    {
        byte[] real = await XlsxFixtures.ExpenseListAsync();

        foreach (int keep in new[] { real.Length / 2, real.Length - 10, 30, 4 })
        {
            Result result = await new ClosedXmlTableReader().ReadAsync(real.AsMemory(0, keep), _options);

            AssertFailure(result, ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonNotAWorkbook);
        }
    }

    [Fact]
    public async Task BinaryGarbageWithZipMagic_IsNotAWorkbook()
    {
        byte[] garbage = new byte[4096];
        new Random(7).NextBytes(garbage);
        garbage[0] = 0x50;
        garbage[1] = 0x4B;
        garbage[2] = 0x03;
        garbage[3] = 0x04;

        Result result = await new ClosedXmlTableReader().ReadAsync(garbage, _options);

        AssertFailure(result, ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonNotAWorkbook);
    }

    [Fact]
    public async Task WorkbookPartsThatAreCorrupt_AreNotAWorkbook()
    {
        byte[] zip = XlsxHandBuilt.Package(
            ("[Content_Types].xml", "not xml at all"),
            ("xl/workbook.xml", "<broken"));

        Result result = await new ClosedXmlTableReader().ReadAsync(zip, _options);

        AssertFailure(result, ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonNotAWorkbook);
    }
}
