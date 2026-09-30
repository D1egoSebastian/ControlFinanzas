namespace ControlDeFinanzasIA.Tests.Integration.Import;

/// <summary>
/// xUnit collection that never runs in parallel with any other collection. The 50,000-row timing gate measures
/// wall-clock time against a fixed ceiling, so it must not compete for CPU and disk with the rest of the suite
/// (F-020, S5, Flakiness rule). The ceiling itself is unchanged.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialPerformanceCollection
{
    public const string Name = "Import serial performance";
}
