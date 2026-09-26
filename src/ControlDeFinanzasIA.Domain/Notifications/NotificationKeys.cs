using System.Globalization;

namespace ControlDeFinanzasIA.Domain.Notifications;

/// <summary>
/// Construye el <c>DedupKey</c> (F-017, D-2) de cada tipo de aviso: la
/// clave de idempotencia única sobre la que se reconcilia
/// <see cref="Notification"/> en cada corrida de detección.
/// </summary>
/// <remarks>
/// Se eligió una única clave de texto en vez de un índice único compuesto
/// sobre <c>(Kind, SourceId, ReferenceDate, Tier)</c>: SQLite trata los
/// <c>NULL</c> como distintos dentro de un índice único, así que toda
/// fila sin <c>Tier</c> (todas menos <c>BudgetThreshold</c>) o sin
/// <c>SourceId</c> (<c>Digest</c>) escaparía la garantía de unicidad.
/// Todas las fechas se formatean con <see cref="CultureInfo.InvariantCulture"/>.
/// </remarks>
public static class NotificationKeys
{
    private const string _dateFormat = "yyyy-MM-dd";
    private const string _monthFormat = "yyyy-MM";

    /// <summary>Clave de un aviso <see cref="NotificationKind.DebtInstallmentDue"/>.</summary>
    /// <param name="paymentId">Id del <c>DebtPayment</c> marcado.</param>
    public static string ForDebtInstallment(Guid paymentId) =>
        $"debt:{paymentId:N}";

    /// <summary>Clave de un aviso <see cref="NotificationKind.RecurringChargeDue"/>.</summary>
    /// <param name="ruleId">Id de la <c>RecurringRule</c> marcada.</param>
    /// <param name="chargeDate"><c>NextGenerationDate</c> al momento de la detección.</param>
    public static string ForRecurringCharge(Guid ruleId, DateOnly chargeDate) =>
        $"recurring:{ruleId:N}:{chargeDate.ToString(_dateFormat, CultureInfo.InvariantCulture)}";

    /// <summary>Clave de un aviso <see cref="NotificationKind.BudgetThreshold"/>.</summary>
    /// <param name="budgetId">Id del <c>Budget</c> marcado.</param>
    /// <param name="monthStart">Primer día del mes evaluado.</param>
    /// <param name="tier">Nivel alcanzado (<see cref="NotificationTier.Near"/> o <see cref="NotificationTier.Exceeded"/>).</param>
    public static string ForBudget(Guid budgetId, DateOnly monthStart, NotificationTier tier) =>
        $"budget:{budgetId:N}:{monthStart.ToString(_monthFormat, CultureInfo.InvariantCulture)}:{tier}";

    /// <summary>Clave de un aviso <see cref="NotificationKind.Anomaly"/>.</summary>
    /// <param name="alertId">Id de la <c>AnomalyAlert</c> (F-014) marcada.</param>
    public static string ForAnomaly(Guid alertId) =>
        $"anomaly:{alertId:N}";

    /// <summary>Clave de un aviso <see cref="NotificationKind.Digest"/>.</summary>
    /// <param name="frequency">Frecuencia configurada (<see cref="DigestFrequency.Daily"/> o <see cref="DigestFrequency.Weekly"/>).</param>
    /// <param name="periodStart">Primer día del período cubierto por el resumen.</param>
    public static string ForDigest(DigestFrequency frequency, DateOnly periodStart) =>
        $"digest:{frequency}:{periodStart.ToString(_dateFormat, CultureInfo.InvariantCulture)}";
}
