namespace ControlDeFinanzasIA.Domain.Notifications;

/// <summary>
/// Frecuencia configurada para el resumen periódico
/// (<see cref="NotificationKind.Digest"/>, F-017, R-13). Se lee y escribe
/// vía <c>SettingKeys</c>/<c>ISettingsStore</c>, nunca vía <c>AppSettings</c>.
/// </summary>
public enum DigestFrequency
{
    /// <summary>El resumen está desactivado: no se generan filas nuevas.</summary>
    Off = 0,

    /// <summary>Un resumen por día calendario.</summary>
    Daily = 1,

    /// <summary>Un resumen por semana ISO (lunes a domingo). Valor por defecto (D17).</summary>
    Weekly = 2
}
