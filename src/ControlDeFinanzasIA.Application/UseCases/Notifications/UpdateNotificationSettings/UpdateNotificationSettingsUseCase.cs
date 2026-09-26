using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;
using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Application.UseCases.Notifications.UpdateNotificationSettings;

/// <summary>
/// Use case que persiste las preferencias de notificaciones (F-017, R-12,
/// R-13, D-7). Cinco escrituras secuenciales vía <see cref="ISettingsStore"/>
/// — nunca vía <c>AppSettings</c> — siguiendo el precedente
/// <c>UpdateAiSettingsUseCase</c>: retorna el primer <see cref="Result"/>
/// fallido y detiene las escrituras restantes.
/// </summary>
public sealed class UpdateNotificationSettingsUseCase
{
    private readonly ISettingsStore _store;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public UpdateNotificationSettingsUseCase(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>
    /// Persiste los cuatro toggles y la frecuencia del resumen.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> exitoso si las cinco claves se persisten; el
    /// primer <see cref="Error"/> no exitoso devuelto por
    /// <see cref="ISettingsStore.SetAsync{T}"/> si una escritura falla.
    /// </returns>
    public async Task<Result> ExecuteAsync(NotificationSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Result debtDueResult = await _store
            .SetAsync(SettingKeys.NotificationsDebtDueEnabled, settings.DebtDue, ct)
            .ConfigureAwait(false);
        if (!debtDueResult.IsSuccess)
        {
            return debtDueResult;
        }

        Result recurringDueResult = await _store
            .SetAsync(SettingKeys.NotificationsRecurringDueEnabled, settings.RecurringDue, ct)
            .ConfigureAwait(false);
        if (!recurringDueResult.IsSuccess)
        {
            return recurringDueResult;
        }

        Result budgetResult = await _store
            .SetAsync(SettingKeys.NotificationsBudgetEnabled, settings.Budget, ct)
            .ConfigureAwait(false);
        if (!budgetResult.IsSuccess)
        {
            return budgetResult;
        }

        Result anomalyResult = await _store
            .SetAsync(SettingKeys.NotificationsAnomalyEnabled, settings.Anomaly, ct)
            .ConfigureAwait(false);
        if (!anomalyResult.IsSuccess)
        {
            return anomalyResult;
        }

        Result digestResult = await _store
            .SetAsync(SettingKeys.NotificationsDigestFrequency, settings.Digest, ct)
            .ConfigureAwait(false);
        if (!digestResult.IsSuccess)
        {
            return digestResult;
        }

        return Result.Success(null!);
    }
}
