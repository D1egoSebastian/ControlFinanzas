using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Integration.Logging;

/// <summary>Settable clock used by the logging tests (F-021 S2).</summary>
internal sealed class SettableClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;

    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
}

/// <summary>A unique, self-cleaning temp directory (never the real AppData).</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cdfia-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of a temp folder.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup of a temp folder.
        }
    }
}

/// <summary>Captures <see cref="Console.Error"/> while alive. Tests using it share one xUnit collection.</summary>
internal sealed class StderrCapture : IDisposable
{
    private readonly TextWriter _original = Console.Error;
    private readonly StringWriter _writer = new();

    public StderrCapture()
    {
        Console.SetError(_writer);
    }

    public string Text => _writer.ToString();

    public void Dispose() => Console.SetError(_original);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StderrCollection
{
    public const string Name = "LoggingStderrAndEnvironment";
}
