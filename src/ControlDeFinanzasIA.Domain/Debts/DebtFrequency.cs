namespace ControlDeFinanzasIA.Domain.Debts;

/// <summary>
/// Frecuencia de las cuotas de una deuda. Define cada cuanto vence
/// una <see cref="DebtPayment"/> dentro del plan.
/// </summary>
public enum DebtFrequency
{
    /// <summary>Una vez al mes (mismo dia del mes, con clamping de fin de mes).</summary>
    Monthly = 0,

    /// <summary>Cada 15 dias exactos, a partir de la fecha de inicio.</summary>
    Biweekly = 1,
}
