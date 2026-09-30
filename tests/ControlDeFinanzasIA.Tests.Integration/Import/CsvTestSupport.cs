using System.Globalization;
using System.Text;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Infrastructure.Import;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>Shared helpers for the CSV reader tests: real bytes in, real <see cref="CsvTableReader"/> out.</summary>
internal static class CsvTestSupport
{
    public static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", name));

    public static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    public static async Task<Result> ReadAsync(byte[] bytes, char? delimiter = null) =>
        await new CsvTableReader().ReadAsync(bytes, new TabularReadOptions(delimiter));

    public static async Task<RawTable> ReadTableAsync(byte[] bytes, char? delimiter = null)
    {
        Result result = await ReadAsync(bytes, delimiter);
        Assert.True(
            result.IsSuccess,
            result.Error?.Code + " " + string.Join(",", result.Error?.Details?.Select(d => d.Key + "=" + d.Value[0]) ?? []));
        return (RawTable)result.Value!;
    }

    public static string[] Texts(RawRow row) => row.Cells.Select(c => c.Text).ToArray();

    public static void AssertFailure(Result result, string code, string? reason = null, int? line = null)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error!.Code);
        if (reason is not null)
        {
            Assert.Equal(reason, result.Error.Details![ImportErrorCodes.DetailReason][0]);
        }

        if (line is not null)
        {
            Assert.Equal(
                line.Value.ToString(CultureInfo.InvariantCulture),
                result.Error.Details![ImportErrorCodes.DetailLine][0]);
        }
    }
}
