using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.Notifications;

/// <summary>
/// Preferencias de notificaciones de la Fase 17 (F-017, R-12, R-13, D-7):
/// los cuatro toggles por tipo detectable y la frecuencia del resumen
/// periódico. Se lee y escribe siempre vía <c>SettingKeys</c>/
/// <see cref="Abstractions.ISettingsStore"/> — nunca vía <c>AppSettings</c>.
/// </summary>
/// <param name="DebtDue">Toggle de avisos <see cref="NotificationKind.DebtInstallmentDue"/>.</param>
/// <param name="RecurringDue">Toggle de avisos <see cref="NotificationKind.RecurringChargeDue"/>.</param>
/// <param name="Budget">Toggle de avisos <see cref="NotificationKind.BudgetThreshold"/>.</param>
/// <param name="Anomaly">Toggle de avisos <see cref="NotificationKind.Anomaly"/>.</param>
/// <param name="Digest">Frecuencia configurada del resumen periódico.</param>
public sealed record NotificationSettings(
    bool DebtDue,
    bool RecurringDue,
    bool Budget,
    bool Anomaly,
    DigestFrequency Digest)
{
    /// <summary>
    /// Valores por defecto (D16/D17): los cuatro toggles encendidos y
    /// frecuencia <see cref="DigestFrequency.Weekly"/>. Un valor ausente o
    /// inválido en el store siempre resuelve a estos defaults.
    /// </summary>
    public static NotificationSettings Defaults { get; } =
        new(DebtDue: true, RecurringDue: true, Budget: true, Anomaly: true, Digest: DigestFrequency.Weekly);
}
