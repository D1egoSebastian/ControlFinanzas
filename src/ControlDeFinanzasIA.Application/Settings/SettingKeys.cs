namespace ControlDeFinanzasIA.Application.Settings;

/// <summary>
/// Claves canonicas usadas para persistir <c>AppSettings</c> como filas
/// <c>Setting(Key, Value)</c>. Los literales importan: la Fase 13 lee
/// estas mismas claves, y cambiarlas mas adelante es una migracion de
/// datos, no un rename (F-012 D-3).
/// </summary>
public static class SettingKeys
{
    /// <summary>Clave de la preferencia de tema.</summary>
    public const string Theme = "theme";

    /// <summary>Clave de la preferencia de idioma.</summary>
    public const string Locale = "locale";

    /// <summary>Clave del proveedor de IA configurado.</summary>
    public const string AiProvider = "ai.provider";

    /// <summary>Clave de la API key del proveedor de IA.</summary>
    public const string AiApiKey = "ai.apiKey";

    /// <summary>Clave del modelo de IA configurado.</summary>
    public const string AiModel = "ai.model";

    /// <summary>Clave del base URL opcional del proveedor de IA.</summary>
    public const string AiBaseUrl = "ai.baseUrl";

    /// <summary>Clave de la temperatura configurada para IA.</summary>
    public const string AiTemperature = "ai.temperature";

    /// <summary>Clave del maximo de tokens por request configurado para IA.</summary>
    public const string AiMaxTokensPerRequest = "ai.maxTokensPerRequest";

    // Fase 17: notificaciones (F-017, R-12, R-13). Bloque agregado — no reordenar lo anterior.

    /// <summary>Clave del toggle de avisos de cuotas de deuda próximas a vencer.</summary>
    public const string NotificationsDebtDueEnabled = "notifications.debtDue.enabled";

    /// <summary>Clave del toggle de avisos de cargos recurrentes próximos a generarse.</summary>
    public const string NotificationsRecurringDueEnabled = "notifications.recurringDue.enabled";

    /// <summary>Clave del toggle de avisos de umbral de presupuesto.</summary>
    public const string NotificationsBudgetEnabled = "notifications.budget.enabled";

    /// <summary>Clave del toggle de avisos de anomalías sin reconocer.</summary>
    public const string NotificationsAnomalyEnabled = "notifications.anomaly.enabled";

    /// <summary>Clave de la frecuencia configurada del resumen periódico.</summary>
    public const string NotificationsDigestFrequency = "notifications.digest.frequency";

    /// <summary>Clave del cursor (inicio del último período generado) del resumen periódico.</summary>
    public const string NotificationsDigestLastPeriodStart = "notifications.digest.lastPeriodStart";
}
