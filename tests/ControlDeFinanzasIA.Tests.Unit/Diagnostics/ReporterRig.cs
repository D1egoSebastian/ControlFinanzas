using ControlDeFinanzasIA.Infrastructure.Logging;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Diagnostics;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Unit.Diagnostics;

/// <summary>
/// The real reporter over the real rolling file provider in a private temp folder and the real overlay state
/// (F-021 S3). Never touches the real AppData.
/// </summary>
internal sealed class ReporterRig : IDisposable
{
    private readonly ILoggerFactory _factory;
    private readonly RollingFileLoggerProvider _provider;

    public ReporterRig()
    {
        Directory = Path.Combine(Path.GetTempPath(), "cdfia-diag-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Clock = new FakeClock { UtcNow = new DateTime(2026, 9, 30, 14, 3, 11, DateTimeKind.Utc) };
        _provider = new RollingFileLoggerProvider(new RollingFileLoggerOptions(Directory), Clock);
        _factory = LoggerFactory.Create(b => b.AddProvider(_provider));
        Overlay = new ErrorOverlayViewModel("1.0.0");
        Reporter = new UnexpectedErrorReporter(_factory.CreateLogger<UnexpectedErrorReporter>(), Clock, Overlay);
    }

    public string Directory { get; }

    public FakeClock Clock { get; }

    public ErrorOverlayViewModel Overlay { get; }

    public UnexpectedErrorReporter Reporter { get; }

    public string[] LogFiles => System.IO.Directory.GetFiles(Directory, "app-*.log");

    public string ReadLog()
    {
        string[] files = LogFiles;
        if (files.Length == 0)
        {
            return string.Empty;
        }

        using var stream = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _provider.Dispose();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of a temp folder.
        }
    }
}
