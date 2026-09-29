using ControlDeFinanzasIA.Application.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Exports;

/// <summary>
/// Tests for <see cref="SaveTarget"/> (F-018, R-1, R-8, D-1): a library-free, disposable
/// handle to the destination the user picked.
/// </summary>
public sealed class SaveTargetTests
{
    private sealed class CountingOwner : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public async Task OpenWriteAsync_DelegatesToSuppliedFunction()
    {
        using var ms = new MemoryStream();
        using var target = new SaveTarget("report.pdf", _ => Task.FromResult<Stream>(ms));

        Stream stream = await target.OpenWriteAsync();

        Assert.Same(ms, stream);
        Assert.Equal("report.pdf", target.DisplayName);
    }

    [Fact]
    public async Task OpenWriteAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        using var target = new SaveTarget("r.xlsx", ct =>
        {
            seen = ct;
            return Task.FromResult<Stream>(new MemoryStream());
        });

        await target.OpenWriteAsync(cts.Token);

        Assert.Equal(cts.Token, seen);
    }

    [Fact]
    public void Dispose_DisposesOwnerExactlyOnce()
    {
        var owner = new CountingOwner();
        var target = new SaveTarget("r.pdf", _ => Task.FromResult<Stream>(new MemoryStream()), owner);

        target.Dispose();
        target.Dispose();

        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public async Task OpenWriteAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var target = new SaveTarget("r.pdf", _ => Task.FromResult<Stream>(new MemoryStream()));
        target.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => target.OpenWriteAsync());
    }

    [Fact]
    public void Dispose_WithoutOwner_DoesNotThrow()
    {
        var target = new SaveTarget("r.pdf", _ => Task.FromResult<Stream>(new MemoryStream()));

        target.Dispose();
    }
}
