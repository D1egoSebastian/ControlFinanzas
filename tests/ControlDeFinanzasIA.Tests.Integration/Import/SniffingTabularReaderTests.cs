using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Infrastructure.Import;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>Content sniffing over the REAL CSV and XLSX readers (F-020, R-2, S-2, D20).</summary>
public sealed class SniffingTabularReaderTests
{
    private static readonly TabularReadOptions _options = new((char?)null);

    private static SniffingTabularReader Reader() => new(new CsvTableReader(), new ClosedXmlTableReader());

    private static void AssertFailure(Result result, string code, string reason)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error!.Code);
        Assert.Equal(reason, result.Error.Details![ImportErrorCodes.DetailReason][0]);
    }

    [Fact]
    public async Task RealWriterXlsx_GoesToTheWorkbookPath()
    {
        Result result = await Reader().ReadAsync(await XlsxFixtures.MonthSummaryAsync(), _options);

        Assert.True(result.IsSuccess);
        var table = (RawTable)result.Value!;
        Assert.Equal(ImportFileKind.Xlsx, table.Source.Kind);
        Assert.Equal(4, table.Rows.Count);
    }

    [Fact]
    public async Task CsvBytes_GoToTheTextPath()
    {
        Result result = await Reader().ReadAsync(CsvTestSupport.Fixture("generic-en-comma-utf8bom.csv"), _options);

        Assert.True(result.IsSuccess);
        Assert.Equal(ImportFileKind.Csv, ((RawTable)result.Value!).Source.Kind);
    }

    [Fact]
    public async Task LegacyXlsMagic_IsLegacyXls()
    {
        byte[] ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];

        Result result = await Reader().ReadAsync(ole, _options);

        AssertFailure(result, ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonLegacyXls);
    }

    [Fact]
    public async Task ZipMagicGarbage_IsNotAWorkbook()
    {
        byte[] garbage = [0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        Result result = await Reader().ReadAsync(garbage, _options);

        AssertFailure(result, ImportErrorCodes.UnreadableFile, ImportErrorCodes.ReasonNotAWorkbook);
    }

    [Fact]
    public async Task SelectionIgnoresNames_SwappedNamesStillReadByContent()
    {
        // The port has no file-name input; the loader-level "swap" is simulating a CSV saved as .xlsx and vice versa.
        byte[] csvNamedXlsx = CsvTestSupport.Fixture("generic-en-comma-utf8bom.csv");
        byte[] xlsxNamedCsv = await XlsxFixtures.ExpenseListAsync();
        SniffingTabularReader reader = Reader();

        var csvTable = (RawTable)(await reader.ReadAsync(csvNamedXlsx, _options)).Value!;
        var xlsxTable = (RawTable)(await reader.ReadAsync(xlsxNamedCsv, _options)).Value!;

        Assert.Equal(ImportFileKind.Csv, csvTable.Source.Kind);
        Assert.Equal(ImportFileKind.Xlsx, xlsxTable.Source.Kind);
        Assert.Equal(
            new[] { typeof(ReadOnlyMemory<byte>), typeof(TabularReadOptions), typeof(CancellationToken) },
            typeof(ITabularReader).GetMethod(nameof(ITabularReader.ReadAsync))!.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [Fact]
    public async Task EmptyContent_FallsThroughToTheTextReader()
    {
        Result result = await Reader().ReadAsync(ReadOnlyMemory<byte>.Empty, _options);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.NoRows, result.Error!.Code);
    }

    [Fact]
    public async Task AddImportReaders_BindsThePortToTheSniffingReader()
    {
        var services = new ServiceCollection();
        services.AddImportReaders();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        ITabularReader port = provider.GetRequiredService<ITabularReader>();
        Result result = await port.ReadAsync(await XlsxFixtures.ExpenseListAsync(), _options);

        Assert.IsType<SniffingTabularReader>(port);
        Assert.Same(port, provider.GetRequiredService<ITabularReader>());
        Assert.True(result.IsSuccess);
    }
}
