using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Fake <see cref="ITabularReader"/> for tests where the reader is a collaborator, not the component under
/// test (F-020, S4b). Serves a fixed table or a fixed failure, or throws.
/// </summary>
public sealed class FakeTabularReader : ITabularReader
{
    private readonly Func<Result> _result;

    public FakeTabularReader(RawTable table)
        : this(() => Result.Success(table))
    {
    }

    public FakeTabularReader(Result result)
        : this(() => result)
    {
    }

    public FakeTabularReader(Func<Result> result)
    {
        _result = result;
    }

    public int Calls { get; private set; }

    public TabularReadOptions? LastOptions { get; private set; }

    public Task<Result> ReadAsync(ReadOnlyMemory<byte> content, TabularReadOptions options, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Calls++;
        LastOptions = options;
        return Task.FromResult(_result());
    }
}
