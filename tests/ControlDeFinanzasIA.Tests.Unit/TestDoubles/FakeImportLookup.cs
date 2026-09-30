using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Fake <see cref="IImportLookup"/> for tests where the lookup is a collaborator, not the component under
/// test (F-020, S4b). Records the requested span and serves a fixed snapshot, a fixed failure, or throws.
/// </summary>
public sealed class FakeImportLookup : IImportLookup
{
    private readonly Func<Result> _result;

    public FakeImportLookup(ImportLookupSnapshot snapshot)
        : this(() => Result.Success(snapshot))
    {
    }

    public FakeImportLookup(Func<Result> result)
    {
        _result = result;
    }

    public int Calls { get; private set; }

    public DateOnly? LastMin { get; private set; }

    public DateOnly? LastMax { get; private set; }

    public static ImportLookupSnapshot Snapshot(
        IEnumerable<CatalogEntry>? categories = null,
        IEnumerable<CatalogEntry>? paymentMethods = null,
        IEnumerable<ExistingTransactionKey>? existing = null) =>
        new(
            (categories ?? DefaultCategories()).ToList(),
            (paymentMethods ?? Array.Empty<CatalogEntry>()).ToList(),
            (existing ?? Array.Empty<ExistingTransactionKey>()).ToList());

    public static IEnumerable<CatalogEntry> DefaultCategories() =>
    [
        new CatalogEntry(new Guid("11111111-1111-1111-1111-000000000001"), "Comida", true),
        new CatalogEntry(ImportLimits.FallbackCategoryId, "Otros", true),
    ];

    public Task<Result> LoadAsync(DateOnly? minDate, DateOnly? maxDate, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Calls++;
        LastMin = minDate;
        LastMax = maxDate;
        return Task.FromResult(_result());
    }
}
