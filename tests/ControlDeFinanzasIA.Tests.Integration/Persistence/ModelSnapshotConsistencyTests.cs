using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Guardia de regresion permanente (F-014, D-5) contra la deriva entre
/// <c>AppDbContextModelSnapshot.cs</c> y el modelo real de EF Core en
/// tiempo de diseño. EF Core 8 no expone <c>HasPendingModelChanges</c>
/// (eso llega en EF 9); esta prueba reimplementa la comparación con
/// <see cref="IMigrationsModelDiffer"/> directamente, que es la única vía
/// disponible en EF 8.
/// </summary>
/// <remarks>
/// Esta es la 5ta fase consecutiva con riesgo de deriva de snapshot
/// (ver <c>AGENTS.md</c>): antes de este test, la única defensa era
/// verificar el snapshot a mano en cada PR. A partir de F-014, la
/// suite lo atrapa automáticamente.
/// </remarks>
public sealed class ModelSnapshotConsistencyTests
{
    [Fact]
    public void DesignTimeModel_MatchesModelSnapshot_NoDrift()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        IMigrationsModelDiffer differ = context.GetService<IMigrationsModelDiffer>();
        IModel snapshotModel = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;

        if (snapshotModel is IMutableModel mutableSnapshotModel)
        {
            snapshotModel = mutableSnapshotModel.FinalizeModel();
        }

        snapshotModel = context.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel);

        bool hasDifferences = differ.HasDifferences(
            snapshotModel.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.False(
            hasDifferences,
            "AppDbContextModelSnapshot.cs is out of sync with the EF Core design-time model. " +
            "Regenerate the pending migration (dotnet ef migrations add <Name>) before merging.");
    }

    [Fact]
    public async Task MigrateAsync_CleanDatabase_CreatesAnomalyAlertsTableAndRoundTrips()
    {
        using var factory = new TestAppDbContextFactory();
        await using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        var alert = Domain.Alerts.AnomalyAlert.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 14),
            Domain.Alerts.AnomalySeverity.Warning,
            "Round-trip guard",
            new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc));

        context.AnomalyAlerts.Add(alert);
        await context.SaveChangesAsync();

        await using AppDbContext freshContext = factory.CreateContext();
        Domain.Alerts.AnomalyAlert? reloaded = await freshContext.AnomalyAlerts.FindAsync(alert.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(Domain.Alerts.AnomalySeverity.Warning, reloaded!.Severity);
    }
}
