using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Notifications;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Notifications;

/// <summary>
/// Tests para <see cref="NotificationSettings.Defaults"/> y
/// <see cref="NotificationSettingsReader"/> (F-017, R-12, R-13, D-7): toda
/// lectura pasa por <c>SettingKeys</c>/<see cref="ISettingsStore"/>, nunca
/// por <c>AppSettings</c>, y un valor ausente o inválido siempre resuelve
/// al default documentado.
/// </summary>
public sealed class NotificationSettingsTests
{
    [Fact]
    public void Defaults_IsAllTogglesOnAndWeeklyDigest()
    {
        NotificationSettings defaults = NotificationSettings.Defaults;

        Assert.True(defaults.DebtDue);
        Assert.True(defaults.RecurringDue);
        Assert.True(defaults.Budget);
        Assert.True(defaults.Anomaly);
        Assert.Equal(DigestFrequency.Weekly, defaults.Digest);
    }

    [Fact]
    public async Task ReadAsync_NoStoredValues_ReadsAllTogglesAsTrueAndWeeklyDigest()
    {
        var store = new InMemorySettingsStore();

        NotificationSettings settings = await NotificationSettingsReader.ReadAsync(store);

        Assert.True(settings.DebtDue);
        Assert.True(settings.RecurringDue);
        Assert.True(settings.Budget);
        Assert.True(settings.Anomaly);
        Assert.Equal(DigestFrequency.Weekly, settings.Digest);
    }

    [Theory]
    [InlineData("not-a-bool")]
    [InlineData("")]
    public async Task ReadAsync_InvalidStoredToggleValue_ReadsAsTrue(string invalidValue)
    {
        var store = new InMemorySettingsStore();
        store.Seed(SettingKeys.NotificationsBudgetEnabled, invalidValue);

        NotificationSettings settings = await NotificationSettingsReader.ReadAsync(store);

        Assert.True(settings.Budget);
    }

    [Fact]
    public async Task ReadAsync_ToggleStoredAsFalse_ReadsAsFalse()
    {
        var store = new InMemorySettingsStore();
        store.Seed(SettingKeys.NotificationsAnomalyEnabled, "false");

        NotificationSettings settings = await NotificationSettingsReader.ReadAsync(store);

        Assert.False(settings.Anomaly);
    }

    [Fact]
    public async Task ReadAsync_InvalidStoredFrequency_ReadsAsWeekly()
    {
        var store = new InMemorySettingsStore();
        store.Seed(SettingKeys.NotificationsDigestFrequency, "not-a-frequency");

        NotificationSettings settings = await NotificationSettingsReader.ReadAsync(store);

        Assert.Equal(DigestFrequency.Weekly, settings.Digest);
    }

    [Fact]
    public async Task ReadAsync_StoredFrequencyDaily_ReadsAsDaily()
    {
        var store = new InMemorySettingsStore();
        store.Seed(SettingKeys.NotificationsDigestFrequency, nameof(DigestFrequency.Daily));

        NotificationSettings settings = await NotificationSettingsReader.ReadAsync(store);

        Assert.Equal(DigestFrequency.Daily, settings.Digest);
    }

    [Fact]
    public void ParseCursor_ValidDate_ReturnsDateOnly()
    {
        DateOnly? cursor = NotificationSettingsReader.ParseCursor("2026-09-22");

        Assert.Equal(new DateOnly(2026, 9, 22), cursor);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("09/22/2026")]
    [InlineData(null)]
    public void ParseCursor_InvalidOrMissingValue_ReturnsNull(string? invalidValue)
    {
        DateOnly? cursor = NotificationSettingsReader.ParseCursor(invalidValue);

        Assert.Null(cursor);
    }
}
