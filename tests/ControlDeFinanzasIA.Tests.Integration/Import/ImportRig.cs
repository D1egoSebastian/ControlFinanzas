using System.Text;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Infrastructure.Import;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// The REAL preview and commit use cases wired over the real reader, lookup and writer on an
/// <see cref="ImportTestDb"/> (F-020, S5). Only the clock is a fake. The reader and writer are wrapped by
/// counting decorators that delegate to the real implementations, so tests can prove they ran without replacing them.
/// </summary>
internal sealed class ImportRig : IDisposable
{
    public static readonly DateOnly Today = new(2026, 9, 30);

    public ImportRig(SaveCountingInterceptor? interceptor = null)
    {
        Db = new ImportTestDb(interceptor: interceptor);
        Reader = new CountingReader(new SniffingTabularReader(new CsvTableReader(), new ClosedXmlTableReader()));
        Writer = new SpyWriter(new EfImportWriter(Db.Scopes, NullLogger<EfImportWriter>.Instance));

        var clock = new ImportTestClock(Today);
        var pipeline = new ImportPipeline(Reader, new EfImportLookup(Db.Scopes, NullLogger<EfImportLookup>.Instance), clock);
        Preview = new PreviewImportUseCase(pipeline, NullLogger<PreviewImportUseCase>.Instance);
        Commit = new CommitImportUseCase(pipeline, Writer, clock, NullLogger<CommitImportUseCase>.Instance);
    }

    public ImportTestDb Db { get; }

    public CountingReader Reader { get; }

    public SpyWriter Writer { get; }

    public PreviewImportUseCase Preview { get; }

    public CommitImportUseCase Commit { get; }

    public static ImportOptions Options(
        bool skipDuplicates = true,
        bool createCategories = true,
        DecimalStyle style = DecimalStyle.Auto,
        ImportDateFormat dateFormat = ImportDateFormat.DayMonthYear) =>
        new(null, null, style, dateFormat, SkipDuplicates: skipDuplicates, CreateMissingCategories: createCategories);

    public static byte[] Csv(params string[] lines) => new UTF8Encoding(false).GetBytes(string.Join("\n", lines) + "\n");

    public static ImportSource Source(byte[] bytes, string name = "import.csv") => new(name, bytes);

    public async Task<ImportPreview> PreviewAsync(byte[] bytes, ImportOptions? options = null)
    {
        Result result = await Preview.ExecuteAsync(Source(bytes), options ?? Options());
        Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
        return (ImportPreview)result.Value!;
    }

    /// <summary>Previews then commits with the previewed hash and the SAME options instance.</summary>
    public async Task<Result> ImportAsync(byte[] bytes, ImportOptions? options = null, CancellationToken ct = default)
    {
        options ??= Options();
        ImportPreview preview = await PreviewAsync(bytes, options);
        return await Commit.ExecuteAsync(Source(bytes), options, preview.PlanHash, ct);
    }

    public async Task<ImportCommitResult> ImportOkAsync(byte[] bytes, ImportOptions? options = null)
    {
        Result result = await ImportAsync(bytes, options);
        Assert.True(result.IsSuccess, result.Error?.Code + " " + result.Error?.Message);
        return (ImportCommitResult)result.Value!;
    }

    public async Task<(List<Expense> Expenses, List<Income> Incomes, List<Category> Categories)> ReadAllAsync()
    {
        await using AsyncServiceScope scope = Db.Scopes.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (
            await db.Expenses.AsNoTracking().OrderBy(e => e.Description).ToListAsync(),
            await db.Incomes.AsNoTracking().OrderBy(i => i.Description).ToListAsync(),
            await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync());
    }

    public (long Expenses, long Incomes, long Categories) Counts() =>
        (Db.Count("Expenses"), Db.Count("Incomes"), Db.Count("Categories"));

    public void Dispose() => Db.Dispose();

    internal sealed class CountingReader(ITabularReader inner) : ITabularReader
    {
        public int Calls { get; private set; }

        public Task<Result> ReadAsync(ReadOnlyMemory<byte> content, TabularReadOptions options, CancellationToken ct = default)
        {
            Calls++;
            return inner.ReadAsync(content, options, ct);
        }
    }

    internal sealed class SpyWriter(IImportWriter inner) : IImportWriter
    {
        public int Calls { get; private set; }

        public Task<Result> WriteAsync(ImportBatch batch, CancellationToken ct = default)
        {
            Calls++;
            return inner.WriteAsync(batch, ct);
        }
    }
}
