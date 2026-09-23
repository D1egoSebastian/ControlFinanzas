using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integracion de round-trip para <see cref="AnomalyAlertRepository"/>
/// contra SQLite real (no in-memory provider), mismo patron que
/// <c>BudgetRepositoryTests</c>.
/// </summary>
public sealed class AnomalyAlertRepositoryTests
{
    private static readonly DateTime _detectedAt = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    private static AnomalyAlert CreateAlert(
        Guid? transactionId = null,
        Guid? categoryId = null,
        DateOnly? date = null,
        AnomalySeverity severity = AnomalySeverity.Warning,
        string message = "Gasto en Supermercado 5.43x el promedio habitual.") =>
        AnomalyAlert.Create(
            transactionId ?? Guid.NewGuid(),
            categoryId ?? Guid.NewGuid(),
            date ?? new DateOnly(2026, 9, 14),
            severity,
            message,
            _detectedAt);

    [Fact]
    public async Task AddAsync_ThenGetById_RoundTripsAllFields()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new AnomalyAlertRepository(context);
        AnomalyAlert alert = CreateAlert();

        Result addResult = await repository.AddAsync(alert);
        AnomalyAlert? loaded = await repository.GetByIdAsync(alert.Id);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(loaded);
        Assert.Equal(alert.TransactionId, loaded!.TransactionId);
        Assert.Equal(alert.CategoryId, loaded.CategoryId);
        Assert.Equal(alert.Date, loaded.Date);
        Assert.Equal(AnomalySeverity.Warning, loaded.Severity);
        Assert.Equal(alert.Message, loaded.Message);
        Assert.Equal(alert.DetectedAt, loaded.DetectedAt);
        Assert.Null(loaded.AcknowledgedAt);
    }

    [Fact]
    public async Task AddAsync_SeverityIsPersistedAsTextByName()
    {
        // Regression guard for D-4: Severity is HasConversion<string>() -- a raw-SQL
        // read must see "Warning"/"Critical" text, never a numeric enum column.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new AnomalyAlertRepository(context);
        AnomalyAlert alert = CreateAlert(severity: AnomalySeverity.Critical);
        await repository.AddAsync(alert);

        // Raw SQL, bypassing the EF model entirely, so a numeric column would
        // surface here even though AnomalyAlert.Severity is a strongly-typed enum.
        await using System.Data.Common.DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Severity FROM AnomalyAlerts";
        object? raw = await command.ExecuteScalarAsync();

        Assert.Equal("Critical", raw);
    }

    [Fact]
    public async Task UniqueIndex_DuplicateTransactionId_ReturnsFailureAndDoesNotPoisonContext()
    {
        // D-4 shared-DbContext gotcha: a failed reconcile must detach only the
        // entries this call attached (never ChangeTracker.Clear()), so a following
        // unrelated save on the SAME context still succeeds.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new AnomalyAlertRepository(context);
        var sharedTransactionId = Guid.NewGuid();
        AnomalyAlert first = CreateAlert(transactionId: sharedTransactionId);
        await repository.AddAsync(first);

        AnomalyAlert duplicate = CreateAlert(transactionId: sharedTransactionId);
        var plan = new AnomalyReconciliationPlan([duplicate], [], []);

        Result reconcileResult = await repository.ApplyReconciliationAsync(plan);

        Assert.False(reconcileResult.IsSuccess);
        Assert.Equal("AnomalyReconcileConflict", reconcileResult.Error!.Code);

        // The shared context must not be poisoned: an unrelated add on it must still succeed.
        AnomalyAlert unrelated = CreateAlert();
        Result unrelatedAddResult = await repository.AddAsync(unrelated);
        Assert.True(unrelatedAddResult.IsSuccess);
    }

    [Fact]
    public async Task GetByMonthAsync_ExcludesOtherMonths()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new AnomalyAlertRepository(context);
        AnomalyAlert september = CreateAlert(date: new DateOnly(2026, 9, 14));
        AnomalyAlert october = CreateAlert(date: new DateOnly(2026, 10, 3));
        await repository.AddAsync(september);
        await repository.AddAsync(october);

        var septemberRange = DateRange.Create(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        IReadOnlyList<AnomalyAlert> results = await repository.GetByMonthAsync(septemberRange);

        Assert.Single(results);
        Assert.Equal(september.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByTransactionIdsAsync_ReturnsMatchingAlertsOnly()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new AnomalyAlertRepository(context);
        AnomalyAlert target = CreateAlert();
        AnomalyAlert other = CreateAlert();
        await repository.AddAsync(target);
        await repository.AddAsync(other);

        IReadOnlyList<AnomalyAlert> results = await repository.GetByTransactionIdsAsync([target.TransactionId]);

        Assert.Single(results);
        Assert.Equal(target.Id, results[0].Id);
    }

    [Fact]
    public async Task ApplyReconciliationAsync_AppliesAddUpdateAndRemoveInOneSaveChanges()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new AnomalyAlertRepository(context);
        AnomalyAlert toUpdate = CreateAlert(severity: AnomalySeverity.Info, message: "before update");
        AnomalyAlert toRemove = CreateAlert();
        await repository.AddAsync(toUpdate);
        await repository.AddAsync(toRemove);

        toUpdate.Refresh(new DateOnly(2026, 9, 20), AnomalySeverity.Critical, "after update");
        AnomalyAlert toAdd = CreateAlert();

        var plan = new AnomalyReconciliationPlan([toAdd], [toUpdate], [toRemove.Id]);
        Result result = await repository.ApplyReconciliationAsync(plan);

        Assert.True(result.IsSuccess);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new AnomalyAlertRepository(freshContext);

        AnomalyAlert? reloadedUpdated = await freshRepository.GetByIdAsync(toUpdate.Id);
        Assert.NotNull(reloadedUpdated);
        Assert.Equal(AnomalySeverity.Critical, reloadedUpdated!.Severity);
        Assert.Equal("after update", reloadedUpdated.Message);

        Assert.Null(await freshRepository.GetByIdAsync(toRemove.Id));
        Assert.NotNull(await freshRepository.GetByIdAsync(toAdd.Id));
    }

    [Fact]
    public async Task ApplyReconciliationAsync_EmptyPlan_DoesNotThrow()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new AnomalyAlertRepository(context);
        var plan = new AnomalyReconciliationPlan([], [], []);

        Result result = await repository.ApplyReconciliationAsync(plan);

        Assert.True(result.IsSuccess);
    }
}
