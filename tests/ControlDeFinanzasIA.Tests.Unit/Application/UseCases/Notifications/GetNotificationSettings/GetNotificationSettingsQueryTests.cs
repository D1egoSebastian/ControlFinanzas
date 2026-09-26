using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotificationSettings;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Notifications.GetNotificationSettings;

/// <summary>
/// Tests para <see cref="GetNotificationSettingsQuery"/> (F-017, R-12, R-13):
/// delega en <see cref="NotificationSettingsReader.ReadAsync"/> y nunca falla.
/// </summary>
public sealed class GetNotificationSettingsQueryTests
{
    [Fact]
    public async Task ExecuteAsync_NoStoredValues_ReturnsDefaults()
    {
        var store = new InMemorySettingsStore();
        var query = new GetNotificationSettingsQuery(store);

        Result result = await query.ExecuteAsync();

        Assert.True(result.IsSuccess);
        NotificationSettings settings = Assert.IsType<NotificationSettings>(result.Value);
        Assert.Equal(NotificationSettings.Defaults, settings);
    }

    [Fact]
    public async Task ExecuteAsync_StoredValues_ReflectsStoredToggles()
    {
        var store = new InMemorySettingsStore();
        store.Seed(SettingKeys.NotificationsBudgetEnabled, "false");
        store.Seed(SettingKeys.NotificationsDigestFrequency, nameof(DigestFrequency.Daily));
        var query = new GetNotificationSettingsQuery(store);

        Result result = await query.ExecuteAsync();

        NotificationSettings settings = Assert.IsType<NotificationSettings>(result.Value);
        Assert.False(settings.Budget);
        Assert.Equal(DigestFrequency.Daily, settings.Digest);
    }
}
