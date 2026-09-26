using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integracion de round-trip para <see cref="NotificationRepository"/>
/// contra SQLite real (no in-memory provider), mismo patron que
/// <c>AnomalyAlertRepositoryTests</c> (F-017, D-8).
/// </summary>
public sealed class NotificationRepositoryTests
{
    private static readonly DateTime _createdAt = new(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc);

    private static Notification CreateDebtNotification(Guid? paymentId = null, DateOnly? dueDate = null) =>
        Notification.ForDebtInstallment(
            paymentId ?? Guid.NewGuid(),
            dueDate ?? new DateOnly(2026, 9, 29),
            "Cuota Auto",
            Money.Create(1500m, Currency.DOP),
            _createdAt);

    [Fact]
    public async Task AddThenGetById_RoundTripsAllFields()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        Notification notification = CreateDebtNotification();

        var plan = new NotificationReconciliationPlan([notification], [], []);
        Result addResult = await repository.ApplyReconciliationAsync(plan);
        Notification? loaded = await repository.GetByIdAsync(notification.Id);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(loaded);
        Assert.Equal(notification.Kind, loaded!.Kind);
        Assert.Equal(notification.DedupKey, loaded.DedupKey);
        Assert.Equal(notification.SourceId, loaded.SourceId);
        Assert.Equal(notification.ReferenceDate, loaded.ReferenceDate);
        Assert.Equal(notification.SubjectLabel, loaded.SubjectLabel);
        Assert.Equal(notification.Amount, loaded.Amount);
        Assert.Equal(notification.Currency, loaded.Currency);
        Assert.Equal(notification.CreatedAt, loaded.CreatedAt);
        Assert.Null(loaded.ReadAt);
        Assert.Null(loaded.DismissedAt);
    }

    [Fact]
    public async Task Add_KindIsPersistedAsTextByName()
    {
        // Regression guard for D-1/D-8: Kind is HasConversion<string>() -- a raw-SQL
        // read must see "DebtInstallmentDue" text, never a numeric enum column.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        Notification notification = CreateDebtNotification();
        await repository.ApplyReconciliationAsync(new NotificationReconciliationPlan([notification], [], []));

        await using System.Data.Common.DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Kind FROM Notifications";
        object? raw = await command.ExecuteScalarAsync();

        Assert.Equal("DebtInstallmentDue", raw);
    }

    [Fact]
    public async Task Add_BudgetNotification_TierIsPersistedAsTextByName()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        var notification = Notification.ForBudget(
            Guid.NewGuid(), new DateOnly(2026, 9, 1), "Comida", 85m, NotificationTier.Near, _createdAt);
        await repository.ApplyReconciliationAsync(new NotificationReconciliationPlan([notification], [], []));

        await using System.Data.Common.DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Tier FROM Notifications";
        object? raw = await command.ExecuteScalarAsync();

        Assert.Equal("Near", raw);
    }

    [Fact]
    public async Task Add_DigestNotification_TotalsRoundTripThroughJson()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        IReadOnlyList<NotificationCurrencyTotal> totals =
        [
            new NotificationCurrencyTotal(Currency.DOP, 2500.50m),
            new NotificationCurrencyTotal(Currency.USD, 100m),
        ];
        var notification = Notification.ForDigest(
            DigestFrequency.Weekly, new DateOnly(2026, 9, 21), payments: 3, totals, budgets: 1, anomalies: 2, _createdAt);
        await repository.ApplyReconciliationAsync(new NotificationReconciliationPlan([notification], [], []));

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new NotificationRepository(freshContext);
        Notification? reloaded = await freshRepository.GetByIdAsync(notification.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(DigestFrequency.Weekly, reloaded!.DigestFrequency);
        Assert.Equal(2, reloaded.DigestTotals.Count);
        Assert.Contains(reloaded.DigestTotals, total => total.Currency == Currency.DOP && total.Amount == 2500.50m);
        Assert.Contains(reloaded.DigestTotals, total => total.Currency == Currency.USD && total.Amount == 100m);
    }

    [Fact]
    public async Task UniqueIndex_DuplicateDedupKey_ReturnsFailureAndDoesNotPoisonContext()
    {
        // Shared-DbContext gotcha (D-1, mirroring AnomalyAlertRepository D-4): a failed
        // reconcile must detach only the entries this call attached (never
        // ChangeTracker.Clear()), so a following unrelated save on the SAME context
        // still succeeds.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        var sharedPaymentId = Guid.NewGuid();
        Notification first = CreateDebtNotification(paymentId: sharedPaymentId);
        await repository.ApplyReconciliationAsync(new NotificationReconciliationPlan([first], [], []));

        Notification duplicate = CreateDebtNotification(paymentId: sharedPaymentId);
        var plan = new NotificationReconciliationPlan([duplicate], [], []);

        Result reconcileResult = await repository.ApplyReconciliationAsync(plan);

        Assert.False(reconcileResult.IsSuccess);
        Assert.Equal("NotificationReconcileConflict", reconcileResult.Error!.Code);

        // The shared context must not be poisoned: an unrelated add on it must still succeed.
        Notification unrelated = CreateDebtNotification();
        Result unrelatedAddResult =
            await repository.ApplyReconciliationAsync(new NotificationReconciliationPlan([unrelated], [], []));
        Assert.True(unrelatedAddResult.IsSuccess);
    }

    [Fact]
    public async Task ApplyReconciliationAsync_AppliesAddUpdateAndRemoveInOneSaveChanges()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        Notification toUpdate = CreateDebtNotification();
        Notification toRemove = CreateDebtNotification();
        await repository.ApplyReconciliationAsync(new NotificationReconciliationPlan([toUpdate, toRemove], [], []));

        var refreshedFacts = Notification.ForDebtInstallment(
            toUpdate.SourceId!.Value,
            new DateOnly(2026, 10, 2),
            "Cuota Auto (actualizada)",
            Money.Create(1600m, Currency.DOP),
            _createdAt);
        toUpdate.RefreshFacts(refreshedFacts);
        Notification toAdd = CreateDebtNotification();

        var plan = new NotificationReconciliationPlan([toAdd], [toUpdate], [toRemove.Id]);
        Result result = await repository.ApplyReconciliationAsync(plan);

        Assert.True(result.IsSuccess);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new NotificationRepository(freshContext);

        Notification? reloadedUpdated = await freshRepository.GetByIdAsync(toUpdate.Id);
        Assert.NotNull(reloadedUpdated);
        Assert.Equal(1600m, reloadedUpdated!.Amount);
        Assert.Equal("Cuota Auto (actualizada)", reloadedUpdated.SubjectLabel);

        Assert.Null(await freshRepository.GetByIdAsync(toRemove.Id));
        Assert.NotNull(await freshRepository.GetByIdAsync(toAdd.Id));
    }

    [Fact]
    public async Task ApplyReconciliationAsync_EmptyPlan_DoesNotThrow()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        var plan = new NotificationReconciliationPlan([], [], []);

        Result result = await repository.ApplyReconciliationAsync(plan);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetUndismissedAsync_ExcludesDismissedRows()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        Notification undismissed = CreateDebtNotification();
        Notification dismissed = CreateDebtNotification();
        dismissed.Dismiss(_createdAt);
        await repository.ApplyReconciliationAsync(
            new NotificationReconciliationPlan([undismissed, dismissed], [], []));

        IReadOnlyList<Notification> results = await repository.GetUndismissedAsync();

        Assert.Single(results);
        Assert.Equal(undismissed.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByIdsAsync_ReturnsMatchingNotificationsOnly()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        Notification target = CreateDebtNotification();
        Notification other = CreateDebtNotification();
        await repository.ApplyReconciliationAsync(new NotificationReconciliationPlan([target, other], [], []));

        IReadOnlyList<Notification> results = await repository.GetByIdsAsync([target.Id]);

        Assert.Single(results);
        Assert.Equal(target.Id, results[0].Id);
    }

    [Fact]
    public async Task UpdateRangeAsync_PersistsMarkReadAndDismiss()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);
        Notification notification = CreateDebtNotification();
        await repository.ApplyReconciliationAsync(new NotificationReconciliationPlan([notification], [], []));

        notification.MarkRead(_createdAt);
        notification.Dismiss(_createdAt);
        Result updateResult = await repository.UpdateRangeAsync([notification]);

        Assert.True(updateResult.IsSuccess);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new NotificationRepository(freshContext);
        Notification? reloaded = await freshRepository.GetByIdAsync(notification.Id);

        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.ReadAt);
        Assert.NotNull(reloaded.DismissedAt);
    }

    [Fact]
    public async Task UpdateRangeAsync_EmptyList_DoesNotThrow()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new NotificationRepository(context);

        Result result = await repository.UpdateRangeAsync([]);

        Assert.True(result.IsSuccess);
    }
}
