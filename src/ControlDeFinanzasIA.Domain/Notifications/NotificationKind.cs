namespace ControlDeFinanzasIA.Domain.Notifications;

/// <summary>
/// Tipos de aviso soportados por la Fase 17 (F-017, R-2). Cada tipo tiene
/// su propia factory en <see cref="Notification"/> y su propia forma de
/// <see cref="NotificationKeys"/>.
/// </summary>
public enum NotificationKind
{
    /// <summary>Cuota de una deuda próxima a vencer o vencida (hasta 7 días).</summary>
    DebtInstallmentDue = 0,

    /// <summary>Cargo de una suscripción o gasto fijo próximo a generarse.</summary>
    RecurringChargeDue = 1,

    /// <summary>Un presupuesto alcanzó el umbral <c>Near</c> (80%) o <c>Exceeded</c> (100%).</summary>
    BudgetThreshold = 2,

    /// <summary>Una <c>AnomalyAlert</c> (F-014) sin reconocer.</summary>
    Anomaly = 3,

    /// <summary>Resumen periódico (diario o semanal) de las demás fuentes.</summary>
    Digest = 4
}
