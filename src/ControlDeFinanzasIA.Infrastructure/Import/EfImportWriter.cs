using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Import;

/// <summary>
/// Persists an <see cref="ImportBatch"/> atomically (F-020, DD-13, R-19). Every call opens its own dependency
/// injection scope and therefore its own <see cref="AppDbContext"/>: new categories, expenses and incomes are
/// staged there and committed by ONE <c>SaveChangesAsync</c>, which EF Core wraps in a single SQLite transaction.
/// A failure rolls all of it back, and because the context dies with the scope no <c>Added</c> entity survives to
/// be flushed by a later unrelated save; the context the ViewModels share is never touched. The work runs in
/// <see cref="Task.Run(Func{Task{Result}}, CancellationToken)"/> because Microsoft.Data.Sqlite completes
/// synchronously and would otherwise block the caller's thread for the whole insert.
/// </summary>
public sealed class EfImportWriter : IImportWriter
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EfImportWriter> _logger;

    /// <summary>Initializes a new instance of the <see cref="EfImportWriter"/> class.</summary>
    /// <param name="scopes">Factory used to open one scope per write.</param>
    /// <param name="logger">Logger for failed writes.</param>
    public EfImportWriter(IServiceScopeFactory scopes, ILogger<EfImportWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(logger);

        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>Writes the batch in one transaction.</summary>
    /// <param name="batch">The entities built by the materializer.</param>
    /// <param name="ct">
    /// Accepted for the port's shape but deliberately not honored: once persistence starts the write runs to
    /// completion or rolls back, so the caller can never observe a half-cancelled import.
    /// </param>
    /// <returns>Success with the <see cref="ImportCommitResult"/> counts written, or <c>WriteFailed</c>.</returns>
    public async Task<Result> WriteAsync(ImportBatch batch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        try
        {
            return await Task.Run(() => PersistAsync(batch), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Writing the import batch failed; the transaction was rolled back");
            return Result.Failure(ImportErrorCodes.WriteFailed, "The import could not be saved. Nothing was written.");
        }
    }

    private async Task<Result> PersistAsync(ImportBatch batch)
    {
        var written = new ImportCommitResult(batch.Expenses.Count, batch.Incomes.Count, batch.NewCategories.Count, 0, 0);

        if (written.ExpensesInserted + written.IncomesInserted + written.CategoriesCreated == 0)
        {
            return Result.Success(written);
        }

        await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        db.Categories.AddRange(batch.NewCategories);
        db.Expenses.AddRange(batch.Expenses);
        db.Incomes.AddRange(batch.Incomes);

        await db.SaveChangesAsync(CancellationToken.None);

        return Result.Success(written);
    }
}
