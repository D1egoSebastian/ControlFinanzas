using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Fake <see cref="IImportWriter"/> for tests where the writer is a collaborator, not the component under test
/// (F-020, S5). Records every batch and token it receives; serves a fixed failure or throws when configured.
/// </summary>
public sealed class FakeImportWriter : IImportWriter
{
    private readonly Func<ImportBatch, Result>? _result;

    public FakeImportWriter()
    {
    }

    public FakeImportWriter(Func<ImportBatch, Result> result)
    {
        _result = result;
    }

    public List<ImportBatch> Batches { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public Task<Result> WriteAsync(ImportBatch batch, CancellationToken ct = default)
    {
        Batches.Add(batch);
        Tokens.Add(ct);
        Result result = _result is null
            ? Result.Success(new ImportCommitResult(batch.Expenses.Count, batch.Incomes.Count, batch.NewCategories.Count, 0, 0))
            : _result(batch);
        return Task.FromResult(result);
    }
}
