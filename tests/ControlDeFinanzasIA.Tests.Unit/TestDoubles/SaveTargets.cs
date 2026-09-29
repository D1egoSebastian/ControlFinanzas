using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Factory of in-memory <see cref="SaveTarget"/> doubles (F-018). <see cref="Recording"/> counts
/// opens and keeps the bytes written, even after the stream is disposed.
/// </summary>
public static class SaveTargets
{
    /// <summary>Creates a recording target, optionally pre-filled with existing content.</summary>
    public static RecordingSaveTarget Recording(byte[]? existingContent = null) => new(existingContent, null);

    /// <summary>Creates a recording target whose open throws the given exception.</summary>
    public static RecordingSaveTarget ThrowingOnOpen(Exception exception) => new(null, exception);

    /// <summary>A save target that records how it was used.</summary>
    public sealed class RecordingSaveTarget : IDisposable
    {
        private readonly MemoryStream _buffer;

        internal RecordingSaveTarget(byte[]? existingContent, Exception? throwOnOpen)
        {
            _buffer = existingContent is null ? new MemoryStream() : new MemoryStream(existingContent.ToArray(), 0, existingContent.Length, writable: true, publiclyVisible: true);
            Target = new SaveTarget(
                "recorded.bin",
                _ =>
                {
                    OpenCount++;
                    if (throwOnOpen is not null)
                    {
                        throw throwOnOpen;
                    }

                    return Task.FromResult<Stream>(_buffer);
                });
        }

        /// <summary>The target to hand to the code under test.</summary>
        public SaveTarget Target { get; }

        /// <summary>How many times the destination was opened.</summary>
        public int OpenCount { get; private set; }

        /// <summary>The bytes currently in the destination (empty when never opened).</summary>
        public byte[] WrittenBytes => _buffer.ToArray();

        public void Dispose()
        {
            Target.Dispose();
            _buffer.Dispose();
        }
    }
}
