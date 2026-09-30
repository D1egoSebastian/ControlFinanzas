using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Import;

/// <summary>
/// Read-only snapshot of what an import needs to know about the database (F-020, DD-11, R-19): the category and
/// payment-method catalogs and the stored expenses and incomes inside the file's date span. Every call opens its
/// own dependency-injection scope and context, so it never touches (and is never disturbed by) the shared
/// <see cref="AppDbContext"/> the rest of the app uses; all queries are <c>AsNoTracking</c>. The work runs in
/// <see cref="Task.Run(Func{Task{ImportLookupSnapshot}}, CancellationToken)"/> because Microsoft.Data.Sqlite
/// completes synchronously and would otherwise block the caller's thread.
/// </summary>
public sealed class EfImportLookup : IImportLookup
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EfImportLookup> _logger;

    /// <summary>Initializes a new instance of the <see cref="EfImportLookup"/> class.</summary>
    /// <param name="scopes">Factory used to open one scope per call.</param>
    /// <param name="logger">Logger for failed lookups.</param>
    public EfImportLookup(IServiceScopeFactory scopes, ILogger<EfImportLookup> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(logger);

        _scopes = scopes;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> LoadAsync(DateOnly? minDate, DateOnly? maxDate, CancellationToken ct = default)
    {
        try
        {
            ImportLookupSnapshot snapshot = await Task.Run(() => QueryAsync(minDate, maxDate, ct), ct);
            return Result.Success(snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Loading the import lookup failed");
            return Result.Failure(ImportErrorCodes.LookupFailed, "The database could not be read to prepare the import.");
        }
    }

    private async Task<ImportLookupSnapshot> QueryAsync(DateOnly? minDate, DateOnly? maxDate, CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<CatalogEntry> categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CatalogEntry(c.Id, c.Name, c.IsSystem))
            .ToListAsync(ct);

        List<CatalogEntry> paymentMethods = await db.PaymentMethods
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new CatalogEntry(p.Id, p.Name, p.IsSystem))
            .ToListAsync(ct);

        var existing = new List<ExistingTransactionKey>();

        if (minDate is DateOnly min && maxDate is DateOnly max)
        {
            existing.AddRange(await db.Expenses
                .AsNoTracking()
                .Where(e => e.Date >= min && e.Date <= max)
                .Select(e => new ExistingTransactionKey(e.Date, TransactionType.Expense, e.Amount.Amount, e.Amount.Currency, e.Description, e.CategoryId))
                .ToListAsync(ct));

            existing.AddRange(await db.Incomes
                .AsNoTracking()
                .Where(i => i.Date >= min && i.Date <= max)
                .Select(i => new ExistingTransactionKey(i.Date, TransactionType.Income, i.Amount.Amount, i.Amount.Currency, i.Description, i.CategoryId))
                .ToListAsync(ct));
        }

        return new ImportLookupSnapshot(categories, paymentMethods, existing);
    }
}
