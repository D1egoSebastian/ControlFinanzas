namespace ControlDeFinanzasIA.Domain.Notifications;

/// <summary>
/// Nivel alcanzado por un <see cref="NotificationKind.BudgetThreshold"/>
/// (F-017, R-5). Solo el nivel más alto vigente tiene una fila sin
/// descartar; alcanzar <see cref="Exceeded"/> retira una fila
/// <see cref="Near"/> sin descartar del mismo presupuesto y mes.
/// </summary>
public enum NotificationTier
{
    /// <summary>El gasto del presupuesto llegó al 80% de su límite.</summary>
    Near = 0,

    /// <summary>El gasto del presupuesto llegó o superó el 100% de su límite.</summary>
    Exceeded = 1
}
