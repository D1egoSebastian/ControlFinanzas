using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Composition;

/// <summary>
/// Startup ordering of <see cref="Bootstrap.RunStartupSequence"/> (F-019, S-25, R-14): the
/// pending-restore completion runs only after a successful migration and never crashes startup.
/// </summary>
public sealed class StartupSequenceTests
{
    [Fact]
    public void RunStartupSequence_RunsMigrationsThenCompletion_InThatOrder()
    {
        var calls = new List<string>();

        Bootstrap.RunStartupSequence(() => calls.Add("migrate"), () => calls.Add("complete"), NullLogger.Instance);

        Assert.Equal(new[] { "migrate", "complete" }, calls);
    }

    [Fact]
    public void RunStartupSequence_WhenMigrationThrows_SkipsCompletionAndRethrows()
    {
        var calls = new List<string>();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Bootstrap.RunStartupSequence(
            () => throw new InvalidOperationException("migration failed"),
            () => calls.Add("complete"),
            NullLogger.Instance));

        Assert.Equal("migration failed", ex.Message);
        Assert.Empty(calls);
    }

    [Fact]
    public void RunStartupSequence_WhenCompletionThrows_DoesNotCrashStartup()
    {
        var calls = new List<string>();

        Bootstrap.RunStartupSequence(
            () => calls.Add("migrate"),
            () => throw new IOException("locked"),
            NullLogger.Instance);

        Assert.Equal(new[] { "migrate" }, calls);
    }

    [Fact]
    public void CompletePendingRestore_ResolvesTheServiceAndCallsIt()
    {
        var fake = new FakeBackupService();
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IBackupService>(fake)
            .BuildServiceProvider();

        Bootstrap.CompletePendingRestore(provider);

        Assert.Equal(1, fake.CompletePendingRestoreCalls);
    }
}
