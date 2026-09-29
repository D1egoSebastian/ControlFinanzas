using ControlDeFinanzasIA.Application.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Backup;

/// <summary>
/// Tests for <see cref="OpenTarget"/> (F-019, R-1, DD-1): a library-free, disposable
/// handle to the file the user picked, mirroring <see cref="SaveTarget"/>.
/// </summary>
public sealed class OpenTargetTests
{
    private sealed class CountingOwner : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public async Task OpenReadAsync_DelegatesToSuppliedFunction_AndExposesDisplayName()
    {
        using var ms = new MemoryStream();
        using var target = new OpenTarget("backup.zip", _ => Task.FromResult<Stream>(ms));

        Stream stream = await target.OpenReadAsync();

        Assert.Same(ms, stream);
        Assert.Equal("backup.zip", target.DisplayName);
    }

    [Fact]
    public async Task OpenReadAsync_IsLazyAndCanBeCalledManyTimes()
    {
        int opens = 0;
        using var target = new OpenTarget("b.zip", _ =>
        {
            opens++;
            return Task.FromResult<Stream>(new MemoryStream());
        });

        Assert.Equal(0, opens);

        await target.OpenReadAsync();
        await target.OpenReadAsync();
        await target.OpenReadAsync();

        Assert.Equal(3, opens);
    }

    [Fact]
    public async Task OpenReadAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        using var target = new OpenTarget("b.zip", ct =>
        {
            seen = ct;
            return Task.FromResult<Stream>(new MemoryStream());
        });

        await target.OpenReadAsync(cts.Token);

        Assert.Equal(cts.Token, seen);
    }

    [Fact]
    public void Dispose_DisposesOwnerExactlyOnce()
    {
        var owner = new CountingOwner();
        var target = new OpenTarget("b.zip", _ => Task.FromResult<Stream>(new MemoryStream()), owner);

        target.Dispose();
        target.Dispose();

        Assert.Equal(1, owner.DisposeCount);
    }

    [Fact]
    public async Task OpenReadAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var target = new OpenTarget("b.zip", _ => Task.FromResult<Stream>(new MemoryStream()));
        target.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => target.OpenReadAsync());
    }

    [Fact]
    public void Dispose_WithoutOwner_DoesNotThrow()
    {
        var target = new OpenTarget("b.zip", _ => Task.FromResult<Stream>(new MemoryStream()));

        target.Dispose();
    }
}
