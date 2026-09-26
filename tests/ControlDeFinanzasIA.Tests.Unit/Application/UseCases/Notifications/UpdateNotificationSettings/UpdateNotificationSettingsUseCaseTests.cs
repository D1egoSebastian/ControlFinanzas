using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.UpdateNotificationSettings;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Notifications.UpdateNotificationSettings;

/// <summary>
/// Tests para <see cref="UpdateNotificationSettingsUseCase"/> (F-017, R-12,
/// R-13, D-7): 5 escrituras secuenciales, retorna el primer fallo (el
/// precedente <c>UpdateAiSettingsUseCase</c>).
/// </summary>
public sealed class UpdateNotificationSettingsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ValidSettings_PersistsAllFiveKeysAndReturnsSuccess()
    {
        var store = new FakeSettingsStore();
        var useCase = new UpdateNotificationSettingsUseCase(store);
        var settings = new NotificationSettings(
            DebtDue: false, RecurringDue: true, Budget: false, Anomaly: true, Digest: DigestFrequency.Daily);

        Result result = await useCase.ExecuteAsync(settings);

        Assert.True(result.IsSuccess);
        // FakeSettingsStore has no dedicated bool case: it serializes via the IFormattable
        // branch, so booleans round-trip as "True"/"False" here (unlike the real
        // SettingsStore.Serialize, which special-cases bool to lowercase). Not modified — it's
        // a shared fake (D-15).
        Assert.Contains((SettingKeys.NotificationsDebtDueEnabled, "False"), store.SetCalls);
        Assert.Contains((SettingKeys.NotificationsRecurringDueEnabled, "True"), store.SetCalls);
        Assert.Contains((SettingKeys.NotificationsBudgetEnabled, "False"), store.SetCalls);
        Assert.Contains((SettingKeys.NotificationsAnomalyEnabled, "True"), store.SetCalls);
        Assert.Contains((SettingKeys.NotificationsDigestFrequency, nameof(DigestFrequency.Daily)), store.SetCalls);
    }

    [Fact]
    public async Task ExecuteAsync_StoreFailsOnThirdKey_ReturnsFirstFailureAndStopsWriting()
    {
        var store = new FakeSettingsStore { FailOnKey = SettingKeys.NotificationsBudgetEnabled };
        var useCase = new UpdateNotificationSettingsUseCase(store);
        NotificationSettings settings = NotificationSettings.Defaults;

        Result result = await useCase.ExecuteAsync(settings);

        Assert.False(result.IsSuccess);
        Assert.Equal("RepositoryError", result.Error!.Code);
        Assert.DoesNotContain(store.SetCalls, c => c.Key == SettingKeys.NotificationsAnomalyEnabled);
        Assert.DoesNotContain(store.SetCalls, c => c.Key == SettingKeys.NotificationsDigestFrequency);
    }
}
