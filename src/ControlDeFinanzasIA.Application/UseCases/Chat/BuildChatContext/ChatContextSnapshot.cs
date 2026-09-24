using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;

/// <summary>
/// Un monto individual (no agregado) del contexto de chat, ya con su
/// conversión a DOP intentada (F-015, D3, R-14). Los agregados (totales de
/// mes, categorías, presupuestos) ya llegan normalizados a DOP desde sus
/// queries de origen; solo los montos por-ítem (metas, deudas, pagos
/// próximos) pasan por este tipo, porque cada uno requiere su propia
/// llamada a <c>ICurrencyConverter.ConvertAsync</c>.
/// </summary>
/// <param name="AmountDop">
/// Monto convertido a DOP, o <see langword="null"/> si la conversión
/// falló. Un <see langword="null"/> aquí NUNCA participa en una suma: el
/// renderizador (Slice 2) lo reemplaza por un token localizado de "monto
/// no disponible" y enciende la bandera de tasas faltantes (R-14).
/// </param>
/// <param name="OriginalAmount">Monto en su moneda original, siempre presente.</param>
/// <param name="OriginalCurrency">Moneda original del monto.</param>
public sealed record ChatAmount(decimal? AmountDop, decimal OriginalAmount, Currency OriginalCurrency);

/// <summary>
/// Totales de un mes calendario, ya normalizados a DOP (R-2). Se usa tanto
/// para el mes actual como para el anterior; <see cref="Month"/> distingue
/// cuál es cuál.
/// </summary>
public sealed record ChatMonthTotals(DateOnly Month, decimal ExpensesDop, decimal IncomesDop, decimal NetBalanceDop);

/// <summary>Una línea de categoría con nombre, ya normalizada a DOP.</summary>
public sealed record ChatCategoryLine(string Name, decimal AmountDop);

/// <summary>
/// El desglose de categorías de un mes, ya recortado al tope de la
/// sección (F-015, D1, reconciliación #1 del spec): <see cref="Lines"/>
/// contiene únicamente categorías con nombre (nunca la porción sintética
/// "Otras" de la query de origen, que no se usa aquí porque se llama con
/// <see cref="ChatContextLimits.UnfoldedTopCount"/>), y el resto se pliega
/// en <see cref="OverflowCount"/>/<see cref="OverflowAmountDop"/> para que
/// el renderizador (Slice 2) arme su propia línea "Otras categorías (N)".
/// </summary>
/// <param name="Month">Mes calendario (primer día) que describe este desglose.</param>
/// <param name="Lines">Categorías con nombre, hasta el tope de la sección, monto descendente.</param>
/// <param name="OverflowCount">Cantidad de categorías con nombre que excedieron el tope. 0 si ninguna.</param>
/// <param name="OverflowAmountDop">Suma DOP de las categorías que excedieron el tope. 0 si <see cref="OverflowCount"/> es 0.</param>
/// <param name="TotalDop">Suma DOP de TODAS las categorías del mes (con y sin nombre en <see cref="Lines"/>).</param>
public sealed record ChatCategoryList(
    DateOnly Month,
    IReadOnlyList<ChatCategoryLine> Lines,
    int OverflowCount,
    decimal OverflowAmountDop,
    decimal TotalDop);

/// <summary>
/// Una línea de presupuesto vs. real, ya normalizada a DOP (R-2, D-8 de
/// F-010). <see cref="Status"/> <see langword="null"/> significa
/// indeterminado (una conversión de moneda falló en algún lado de la
/// comparación) y NUNCA debe leerse como 0% ni 100%.
/// </summary>
public sealed record ChatBudgetLine(
    string CategoryName,
    decimal? BudgetDop,
    decimal? ActualDop,
    decimal? PercentConsumed,
    BudgetStatus? Status);

/// <summary>Una meta de ahorro activa, con sus montos ya pasados por <see cref="ChatAmount"/> (F-015, D3).</summary>
public sealed record ChatGoalLine(
    string Name,
    ChatAmount Target,
    ChatAmount Contributed,
    ChatAmount Remaining,
    decimal PercentComplete,
    DateOnly TargetDate,
    ChatAmount MonthlyRequired);

/// <summary>Una deuda activa, con su saldo pendiente y cuota ya pasados por <see cref="ChatAmount"/> (F-015, D3).</summary>
/// <param name="Name">Descripción de la deuda (ej. "Préstamo carro"). NUNCA <c>Debt.CounterpartyName</c> ni <c>Debt.Note</c> (R-11, D-2 privacidad).</param>
/// <param name="PendingBalance">Saldo pendiente, calculado por <c>Debt.CalculatePendingBalance</c> (Domain).</param>
/// <param name="Installment">Monto de cada cuota.</param>
/// <param name="PendingInstallments">Cantidad de cuotas en estado <c>Pending</c>.</param>
/// <param name="NextDueDate">Fecha de la próxima cuota pendiente, o <see langword="null"/> si no queda ninguna.</param>
public sealed record ChatDebtLine(
    string Name,
    ChatAmount PendingBalance,
    ChatAmount Installment,
    int PendingInstallments,
    DateOnly? NextDueDate);

/// <summary>Un pago próximo (cuota de deuda o generación de regla recurrente), con su monto ya pasado por <see cref="ChatAmount"/>.</summary>
public sealed record ChatPaymentLine(DateOnly Date, string Label, UpcomingPaymentKind Kind, ChatAmount Amount);

/// <summary>
/// Distingue cada sección del contexto financiero, para el registro de
/// fallas por sección (F-015, D1) y para el orden fijo de recorte del
/// renderizador (D6, Slice 2).
/// </summary>
public enum ChatContextSection
{
    /// <summary>Totales del mes actual.</summary>
    CurrentMonthSummary,

    /// <summary>Desglose de categorías del mes actual.</summary>
    CurrentMonthCategories,

    /// <summary>Totales del mes anterior.</summary>
    PriorMonthSummary,

    /// <summary>Desglose de categorías del mes anterior.</summary>
    PriorMonthCategories,

    /// <summary>Presupuesto vs. real del mes actual.</summary>
    Budgets,

    /// <summary>Metas de ahorro activas.</summary>
    Goals,

    /// <summary>Deudas activas.</summary>
    Debts,

    /// <summary>Pagos próximos dentro del horizonte configurado.</summary>
    UpcomingPayments,
}

/// <summary>
/// Snapshot format-agnóstico del contexto financiero para el chat con IA
/// (F-015, D1, D2). Cada sección es <see langword="null"/> cuando el paso
/// que la produce falló (no disponible) o una lista/record poblado en
/// caso contrario; una lista vacía (p. ej. <see cref="Goals"/> = <c>[]</c>)
/// significa "sin elementos", un dato legítimo y distinto de "no
/// disponible". <see cref="ChatContextRenderer"/> (Slice 2) es el único
/// consumidor de este tipo para producir texto; la UI (Slice 5) lo lee
/// directamente para el panel de cifras exactas (R-10).
/// </summary>
/// <param name="Today">Fecha "hoy" resuelta desde <c>IClock</c> al momento de construir el snapshot (D9: nunca el mes navegado del Dashboard).</param>
/// <param name="CurrentMonth">Totales del mes actual, o <see langword="null"/> si el paso falló.</param>
/// <param name="PriorMonth">Totales del mes anterior, o <see langword="null"/> si el paso falló.</param>
/// <param name="CurrentCategories">Desglose de categorías del mes actual, o <see langword="null"/> si el paso falló.</param>
/// <param name="PriorCategories">Desglose de categorías del mes anterior, o <see langword="null"/> si el paso falló.</param>
/// <param name="Budgets"><see langword="null"/> = sección no disponible; lista vacía = sin presupuestos este mes.</param>
/// <param name="Goals"><see langword="null"/> = sección no disponible; lista vacía = sin metas activas.</param>
/// <param name="Debts"><see langword="null"/> = sección no disponible; lista vacía = sin deudas activas.</param>
/// <param name="UpcomingPayments"><see langword="null"/> = sección no disponible; lista vacía = sin pagos próximos.</param>
/// <param name="HasMissingRates">
/// OR de la bandera de tasas faltantes de cada sección agregada MÁS
/// cualquier conversión por-ítem fallida (metas, deudas, pagos). Nunca se
/// deriva del recorte de secciones (eso es responsabilidad del
/// renderizador, no de este snapshot).
/// </param>
public sealed record ChatContextSnapshot(
    DateOnly Today,
    ChatMonthTotals? CurrentMonth,
    ChatMonthTotals? PriorMonth,
    ChatCategoryList? CurrentCategories,
    ChatCategoryList? PriorCategories,
    IReadOnlyList<ChatBudgetLine>? Budgets,
    IReadOnlyList<ChatGoalLine>? Goals,
    IReadOnlyList<ChatDebtLine>? Debts,
    IReadOnlyList<ChatPaymentLine>? UpcomingPayments,
    bool HasMissingRates);
