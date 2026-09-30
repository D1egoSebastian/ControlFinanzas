using System.Diagnostics;
using System.Globalization;
using System.Text;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using Xunit.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// The 50,000-row decision gate (F-020, S5, S-29, DD-13): the REAL reader, planner, materializer and writer on a
/// real SQLite file must import the maximum file inside <see cref="_ceilingSeconds"/> seconds. The ceiling is the
/// documented contract; it is never raised to make a run pass.
/// </summary>
[Collection(SerialPerformanceCollection.Name)]
public sealed class ImportPerformanceTests(ITestOutputHelper output)
{
    private const int _ceilingSeconds = 30;

    private static byte[] BuildCsv(int rows)
    {
        var text = new StringBuilder(rows * 60);
        text.Append("Date,Type,Description,Category,Amount,Currency\n");
        var start = new DateOnly(2020, 1, 1);

        for (int i = 0; i < rows; i++)
        {
            DateOnly date = start.AddDays(i % 2000);
            string type = i % 10 == 0 ? "Income" : "Expense";
            text.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(type).Append(',')
                .Append("Movement ").Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(i % 3 == 0 ? "Comida" : "Category " + (i % 20).ToString("00", CultureInfo.InvariantCulture)).Append(',')
                .Append((1 + (i % 9973)).ToString(CultureInfo.InvariantCulture)).Append('.').Append((i % 100).ToString("00", CultureInfo.InvariantCulture)).Append(',')
                .Append(i % 7 == 0 ? "USD" : "DOP").Append('\n');
        }

        return new UTF8Encoding(false).GetBytes(text.ToString());
    }

    [Fact]
    public async Task Commit_FiftyThousandRows_FinishesInsideTheCeilingWithOneSave()
    {
        const int rows = ImportLimits.MaxDataRows;
        byte[] bytes = BuildCsv(rows);
        var saves = new SaveCountingInterceptor();
        using var rig = new ImportRig(saves);
        ImportOptions options = ImportRig.Options();
        (long Expenses, long Incomes, long Categories) before = rig.Counts();

        var previewWatch = Stopwatch.StartNew();
        ImportPreview preview = await rig.PreviewAsync(bytes, options);
        previewWatch.Stop();
        Assert.Equal(rows, preview.ToImportCount);

        var commitWatch = Stopwatch.StartNew();
        Result result = await rig.Commit.ExecuteAsync(ImportRig.Source(bytes), options, preview.PlanHash);
        commitWatch.Stop();

        output.WriteLine($"50k rows: preview {previewWatch.Elapsed.TotalSeconds:F2} s, commit (re-plan + build + write) {commitWatch.Elapsed.TotalSeconds:F2} s");
        Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
        var written = (ImportCommitResult)result.Value!;
        Assert.Equal(rows - (rows / 10 + (rows % 10 == 0 ? 0 : 1)), written.ExpensesInserted);
        Assert.Equal(rows / 10 + (rows % 10 == 0 ? 0 : 1), written.IncomesInserted);
        Assert.Equal(20, written.CategoriesCreated);
        Assert.Equal((before.Expenses + written.ExpensesInserted, before.Incomes + written.IncomesInserted, before.Categories + 20), rig.Counts());
        Assert.Equal(rows, rig.Db.Count("Expenses") + rig.Db.Count("Incomes") - before.Expenses - before.Incomes);
        Assert.True(commitWatch.Elapsed < TimeSpan.FromSeconds(_ceilingSeconds), $"Commit took {commitWatch.Elapsed.TotalSeconds:F1} s (ceiling {_ceilingSeconds} s)");
        Assert.Equal(1, saves.Saves);
    }
}
