namespace ControlDeFinanzasIA.Domain.Goals;

/// <summary>
/// Estado de una <see cref="Goal"/>. Una meta nace <see cref="Active"/>
/// y pasa a <see cref="Completed"/> automaticamente cuando sus
/// contribuciones alcanzan o superan el monto objetivo (R-8).
/// </summary>
public enum GoalStatus
{
    /// <summary>La meta aun no alcanza su monto objetivo.</summary>
    Active = 0,

    /// <summary>La meta alcanzo o supero su monto objetivo.</summary>
    Completed = 1,
}
