using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>Tests for <see cref="ImportSourceLoader"/> (F-020, R-2, R-6, C-22).</summary>
public sealed class ImportSourceLoaderTests
{
    private static byte[] Bytes(int length)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        return data;
    }

    [Fact]
    public async Task LoadAsync_ExactlyMaxFileBytes_PassesAndRetainsInput()
    {
        byte[] input = Bytes(ImportLimits.MaxFileBytes);
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording(input);

        Result result = await ImportSourceLoader.LoadAsync(recording.Target);

        Assert.True(result.IsSuccess);
        ImportSource source = Assert.IsType<ImportSource>(result.Value);
        Assert.True(input.AsSpan().SequenceEqual(source.Content.Span));
        Assert.Equal("recorded.zip", source.DisplayName);
    }

    [Fact]
    public async Task LoadAsync_OneByteOverCap_FailsTooLargeWithReason()
    {
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording(Bytes(ImportLimits.MaxFileBytes + 1));

        Result result = await ImportSourceLoader.LoadAsync(recording.Target);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.TooLarge, result.Error!.Code);
        Assert.Equal(ImportErrorCodes.ReasonFileTooLarge, result.Error.Details![ImportErrorCodes.DetailReason][0]);
    }

    [Fact]
    public async Task LoadAsync_OverCap_NeverReadsMoreThanCapPlusOneByte()
    {
        using var stream = new ScriptedStream(Bytes(ImportLimits.MaxFileBytes + 50_000), chunk: 70_000);
        using var target = new OpenTarget("big.csv", _ => Task.FromResult<Stream>(stream));

        Result result = await ImportSourceLoader.LoadAsync(target);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.TooLarge, result.Error!.Code);
        Assert.True(stream.BytesServed <= (long)ImportLimits.MaxFileBytes + 1, $"served {stream.BytesServed}");
    }

    [Fact]
    public async Task LoadAsync_NonSeekableStreamWithUnknownLength_ReadsToEndWithoutTruncation()
    {
        byte[] input = Bytes(300_000);
        using var stream = new ScriptedStream(input, chunk: 1000);
        using var target = new OpenTarget("pipe.csv", _ => Task.FromResult<Stream>(stream));

        Result result = await ImportSourceLoader.LoadAsync(target);

        Assert.True(result.IsSuccess);
        ImportSource source = Assert.IsType<ImportSource>(result.Value);
        Assert.True(input.AsSpan().SequenceEqual(source.Content.Span));
    }

    [Fact]
    public async Task LoadAsync_EmptyStream_SucceedsWithEmptyContent()
    {
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording();

        Result result = await ImportSourceLoader.LoadAsync(recording.Target);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, Assert.IsType<ImportSource>(result.Value).Content.Length);
    }

    [Fact]
    public async Task LoadAsync_OpensExactlyOnce_AndDisposesTarget()
    {
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording(Bytes(10));

        await ImportSourceLoader.LoadAsync(recording.Target);

        Assert.Equal(1, recording.OpenCount);
        Assert.Equal(1, recording.DisposeCount);
    }

    [Fact]
    public async Task LoadAsync_OnFailure_StillDisposesTarget()
    {
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording(Bytes(ImportLimits.MaxFileBytes + 1));

        await ImportSourceLoader.LoadAsync(recording.Target);

        Assert.Equal(1, recording.OpenCount);
        Assert.Equal(1, recording.DisposeCount);
    }

    [Fact]
    public async Task LoadAsync_IoErrorWhileOpening_ReturnsUnreadableFile()
    {
        using var target = new OpenTarget("locked.csv", _ => throw new IOException("locked"));

        Result result = await ImportSourceLoader.LoadAsync(target);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportErrorCodes.UnreadableFile, result.Error!.Code);
    }

    [Fact]
    public async Task LoadAsync_Cancelled_PropagatesOperationCanceled()
    {
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording(Bytes(10));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ImportSourceLoader.LoadAsync(recording.Target, cts.Token));
    }

    /// <summary>Non-seekable stream that serves the input in bounded chunks and counts what it served.</summary>
    private sealed class ScriptedStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _chunk;
        private int _position;

        public ScriptedStream(byte[] data, int chunk)
        {
            _data = data;
            _chunk = chunk;
        }

        public long BytesServed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int n = Math.Min(Math.Min(buffer.Length, _chunk), _data.Length - _position);
            _data.AsSpan(_position, n).CopyTo(buffer);
            _position += n;
            BytesServed += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
