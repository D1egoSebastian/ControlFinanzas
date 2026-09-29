using System.Runtime.CompilerServices;
using LiveChartsCore.SkiaSharpView;

namespace ControlDeFinanzasIA.Tests.Unit.Common;

/// <summary>
/// Forces LiveCharts' one-time global initialisation to run single-threaded
/// before any test starts. LiveCharts lazily configures its process-wide
/// settings (a plain <c>Dictionary</c> of mappers) from the static
/// constructor of the first series type touched; xUnit runs test classes in
/// parallel, so several <c>DashboardViewModel</c> loads used to race on that
/// dictionary and intermittently threw <see cref="TypeInitializationException"/>
/// (in production the first chart is always created on the UI thread).
/// </summary>
internal static class LiveChartsWarmup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        _ = new PieSeries<double>();
        _ = new LineSeries<double>();
    }
}
