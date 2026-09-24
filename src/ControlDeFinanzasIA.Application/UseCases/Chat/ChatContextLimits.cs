namespace ControlDeFinanzasIA.Application.UseCases.Chat;

/// <summary>
/// Constantes con nombre que acotan el contexto financiero enviado al
/// modelo (F-015, D1, D6): tope de elementos por sección y presupuesto
/// total de caracteres. Centralizadas aquí para que
/// <c>BuildChatContextQuery</c> (recorte por sección) y
/// <c>ChatContextRenderer</c> (recorte final de presupuesto, Slice 2) usen
/// exactamente los mismos valores.
/// </summary>
public static class ChatContextLimits
{
    /// <summary>
    /// Categorías con nombre incluidas del mes actual, antes de la línea
    /// de desbordamiento "Otras categorías (N)" (D1, S-1: ninguna
    /// categoría dentro de este tope queda oculta, a diferencia del
    /// top-5 del Dashboard).
    /// </summary>
    public const int CurrentMonthCategories = 30;

    /// <summary>Categorías con nombre incluidas del mes anterior, antes del desbordamiento.</summary>
    public const int PriorMonthCategories = 10;

    /// <summary>Líneas de presupuesto vs. real incluidas, en el orden que ya produce la query.</summary>
    public const int Budgets = 15;

    /// <summary>Metas activas incluidas.</summary>
    public const int Goals = 10;

    /// <summary>Deudas activas incluidas.</summary>
    public const int Debts = 10;

    /// <summary>Pagos próximos incluidos.</summary>
    public const int UpcomingPayments = 10;

    /// <summary>Horizonte, en días desde <c>IClock.Today</c>, de la sección de pagos próximos.</summary>
    public const int UpcomingHorizonDays = 30;

    /// <summary>Presupuesto máximo de caracteres del contexto renderizado (D6, Slice 2).</summary>
    public const int MaxContextChars = 6_000;

    /// <summary>Longitud máxima de un nombre saneado antes de truncar con "…" (Slice 2).</summary>
    public const int MaxNameChars = 80;

    /// <summary>
    /// <c>topCount</c> enviado a <c>GetCategoryBreakdownQuery</c> para que la query nunca
    /// folde internamente: el builder mismo arma la línea "Otras categorías (N)" a partir
    /// de las categorías que exceden <see cref="CurrentMonthCategories"/>/<see cref="PriorMonthCategories"/>,
    /// preservando el conteo exacto que la query descartaría si se le pasara <c>topCount = 30</c>
    /// (ver design.md D-1, "Why topCount = int.MaxValue and not 30").
    /// </summary>
    public const int UnfoldedTopCount = int.MaxValue;
}
