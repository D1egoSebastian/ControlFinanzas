using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.Notifications;

/// <summary>
/// Lee <see cref="NotificationSettings"/> desde <see cref="ISettingsStore"/>
/// (F-017, R-12, R-13, D-7). Un valor ausente o inválido nunca lanza:
/// siempre resuelve al default documentado en cada método.
/// </summary>
public static class NotificationSettingsReader
{
    private const string _cursorFormat = "yyyy-MM-dd";

    /// <summary>
    /// Lee los cuatro toggles por tipo y la frecuencia del resumen.
    /// </summary>
    /// <param name="store">Store del que leer las claves de <see cref="SettingKeys"/>.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Las <see cref="NotificationSettings"/> vigentes. Un toggle ausente o
    /// con un valor que no parsea como <see cref="bool"/> se lee como
    /// <see langword="true"/>; una frecuencia ausente o inválida se lee
    /// como <see cref="DigestFrequency.Weekly"/>.
    /// </returns>
    public static async Task<NotificationSettings> ReadAsync(ISettingsStore store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        bool debtDue = await ReadToggleAsync(store, SettingKeys.NotificationsDebtDueEnabled, ct).ConfigureAwait(false);
        bool recurringDue = await ReadToggleAsync(store, SettingKeys.NotificationsRecurringDueEnabled, ct).ConfigureAwait(false);
        bool budget = await ReadToggleAsync(store, SettingKeys.NotificationsBudgetEnabled, ct).ConfigureAwait(false);
        bool anomaly = await ReadToggleAsync(store, SettingKeys.NotificationsAnomalyEnabled, ct).ConfigureAwait(false);
        DigestFrequency digest = await store
            .GetEnumAsync(SettingKeys.NotificationsDigestFrequency, DigestFrequency.Weekly, ct)
            .ConfigureAwait(false);

        return new NotificationSettings(debtDue, recurringDue, budget, anomaly, digest);
    }

    /// <summary>
    /// Parsea el cursor del resumen (<c>notifications.digest.lastPeriodStart</c>),
    /// persistido como un string <c>yyyy-MM-dd</c> (D-7). Consumido por
    /// <c>DetectNotificationsUseCase</c> (F-017 slice S7).
    /// </summary>
    /// <param name="raw">Valor crudo leído del store, o <see langword="null"/>.</param>
    /// <returns>
    /// El <see cref="DateOnly"/> parseado, o <see langword="null"/> si
    /// <paramref name="raw"/> es ausente o no tiene el formato esperado.
    /// </returns>
    public static DateOnly? ParseCursor(string? raw) =>
        DateOnly.TryParseExact(raw, _cursorFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly cursor)
            ? cursor
            : null;

    private static async Task<bool> ReadToggleAsync(ISettingsStore store, string key, CancellationToken ct)
    {
        string? raw = await store.GetStringAsync(key, defaultValue: null, ct).ConfigureAwait(false);
        return !bool.TryParse(raw, out bool value) || value;
    }
}
