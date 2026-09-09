namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Frecuencia de una regla recurrente. Define cada cuanto se genera
/// un movimiento.
/// </summary>
public enum RecurringFrequency
{
    /// <summary>Una vez al mes (cada N meses, default N=1).</summary>
    Monthly = 0,

    /// <summary>Una vez cada dos meses.</summary>
    Bimonthly = 1,

    /// <summary>Una vez al a~no.</summary>
    Yearly = 2,
}
