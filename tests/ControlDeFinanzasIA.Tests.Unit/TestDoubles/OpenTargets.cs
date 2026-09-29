using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Factory of in-memory <see cref="OpenTarget"/> doubles (F-019). <see cref="Recording"/> counts
/// opens and disposals and serves the supplied bytes.
/// </summary>
public static class OpenTargets
{
    /// <summary>Creates a recording target serving <paramref name="content"/>.</summary>
    public static RecordingOpenTarget Recording(byte[]? content = null) => new(content ?? Array.Empty<byte>());

    /// <summary>An open target that records how it was used.</summary>
    public sealed class RecordingOpenTarget : IDisposable
    {
        private readonly byte[] _content;

        internal RecordingOpenTarget(byte[] content)
        {
            _content = content;
            Target = new OpenTarget(
                "recorded.zip",
                _ =>
                {
                    OpenCount++;
                    return Task.FromResult<Stream>(new MemoryStream(_content, writable: false));
                },
                new DisposalCounter(this));
        }

        /// <summary>The target to hand to the code under test.</summary>
        public OpenTarget Target { get; }

        /// <summary>How many times the source was opened.</summary>
        public int OpenCount { get; private set; }

        /// <summary>How many times the target released its owner.</summary>
        public int DisposeCount { get; private set; }

        public void Dispose() => Target.Dispose();

        private sealed class DisposalCounter : IDisposable
        {
            private readonly RecordingOpenTarget _owner;

            public DisposalCounter(RecordingOpenTarget owner)
            {
                _owner = owner;
            }

            public void Dispose() => _owner.DisposeCount++;
        }
    }
}
