using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;

/// <summary>
/// Etiquetas y plantillas de formato del contexto financiero renderizado
/// para el chat (F-015, D-4). Son DATOS DE PROMPT, no copy de UI (R-15,
/// reconciliación #5 del spec): viven en Application, junto a
/// <see cref="ChatContextRenderer"/>, no en <c>Strings.resx</c>. Los
/// marcadores estructurales <c>##</c>/<c>!!</c> y las cabeceras
/// <c>=== FINANCIAL CONTEXT ===</c>/<c>=== END FINANCIAL CONTEXT ===</c>
/// son neutrales al idioma y viven en <see cref="ChatContextRenderer"/>.
/// </summary>
/// <param name="TodayFormat">"Hoy: {0}. Montos en DOP salvo indicación." con <c>{0}</c> = fecha ISO.</param>
/// <param name="MissingRatesFlag">Línea de bandera cuando <c>HasMissingRates</c> es verdadero (D-14).</param>
/// <param name="CurrentMonthBaseLabel">Etiqueta genérica del mes actual, sin fecha (para estados no disponible/omitida).</param>
/// <param name="CurrentMonthSectionFormat">"## Mes actual: {0}" con <c>{0}</c> = texto de mes.</param>
/// <param name="PriorMonthBaseLabel">Etiqueta genérica del mes anterior.</param>
/// <param name="PriorMonthSectionFormat">"## Mes anterior: {0}".</param>
/// <param name="ExpensesLabel">Etiqueta de gastos dentro de un bloque de totales.</param>
/// <param name="IncomesLabel">Etiqueta de ingresos dentro de un bloque de totales.</param>
/// <param name="NetBalanceLabel">Etiqueta de balance neto dentro de un bloque de totales.</param>
/// <param name="CurrentCategoriesBaseLabel">Etiqueta genérica de categorías del mes actual.</param>
/// <param name="CurrentCategoriesSectionFormat">"## Gastos por categoría, {0}".</param>
/// <param name="PriorCategoriesBaseLabel">Etiqueta genérica de categorías del mes anterior.</param>
/// <param name="PriorCategoriesSectionFormat">"## Gastos por categoría, {0} (top 10)".</param>
/// <param name="OverflowLineFormat">"Otras categorías ({0}): {1}" con conteo y monto DOP.</param>
/// <param name="BudgetsBaseLabel">Etiqueta genérica de presupuestos.</param>
/// <param name="BudgetsSectionFormat">"## Presupuestos, {0}".</param>
/// <param name="BudgetLineFormat">Línea completa de una categoría presupuestada.</param>
/// <param name="BudgetIndeterminateFormat">Línea de una categoría con estado indeterminado (D-2, F-010).</param>
/// <param name="BudgetStatusOk"><see cref="Domain.Budgets.BudgetStatus.Ok"/> localizado.</param>
/// <param name="BudgetStatusNear"><see cref="Domain.Budgets.BudgetStatus.Near"/> localizado.</param>
/// <param name="BudgetStatusExceeded"><see cref="Domain.Budgets.BudgetStatus.Exceeded"/> localizado.</param>
/// <param name="GoalsBaseLabel">Etiqueta genérica de metas activas.</param>
/// <param name="GoalsSection">"## Metas activas" (sin componente dinámico).</param>
/// <param name="GoalLineFormat">Línea completa de una meta activa.</param>
/// <param name="DebtsBaseLabel">Etiqueta genérica de deudas activas.</param>
/// <param name="DebtsSection">"## Deudas activas" (sin componente dinámico).</param>
/// <param name="DebtLineFormat">Línea completa de una deuda activa.</param>
/// <param name="NoNextDueDateToken">Token para una deuda sin cuotas pendientes.</param>
/// <param name="PaymentsBaseLabel">Etiqueta genérica de pagos próximos.</param>
/// <param name="PaymentsSectionFormat">"## Pagos próximos ({0} días)" con el horizonte configurado.</param>
/// <param name="PaymentLineFormat">Línea completa de un pago próximo.</param>
/// <param name="DebtInstallmentTag">Sufijo de un pago que proviene de una cuota de deuda.</param>
/// <param name="RecurringTag">Sufijo de un pago que proviene de una regla recurrente.</param>
/// <param name="SectionEmptySuffix">Sufijo "ninguna" — la sección corrió y no hay elementos (dato legítimo).</param>
/// <param name="SectionUnavailableSuffix">Sufijo "no disponible" — la sección falló al cargar.</param>
/// <param name="SectionOmittedSuffix">Sufijo "omitida" — el recorte de presupuesto la descartó (D-4, S-5).</param>
/// <param name="AmountUnavailableToken">Token para un <see cref="ChatAmount"/> cuya conversión a DOP falló (R-14).</param>
public sealed record ChatContextLabelSet(
    string TodayFormat,
    string MissingRatesFlag,
    string CurrentMonthBaseLabel,
    string CurrentMonthSectionFormat,
    string PriorMonthBaseLabel,
    string PriorMonthSectionFormat,
    string ExpensesLabel,
    string IncomesLabel,
    string NetBalanceLabel,
    string CurrentCategoriesBaseLabel,
    string CurrentCategoriesSectionFormat,
    string PriorCategoriesBaseLabel,
    string PriorCategoriesSectionFormat,
    string OverflowLineFormat,
    string BudgetsBaseLabel,
    string BudgetsSectionFormat,
    string BudgetLineFormat,
    string BudgetIndeterminateFormat,
    string BudgetStatusOk,
    string BudgetStatusNear,
    string BudgetStatusExceeded,
    string GoalsBaseLabel,
    string GoalsSection,
    string GoalLineFormat,
    string DebtsBaseLabel,
    string DebtsSection,
    string DebtLineFormat,
    string NoNextDueDateToken,
    string PaymentsBaseLabel,
    string PaymentsSectionFormat,
    string PaymentLineFormat,
    string DebtInstallmentTag,
    string RecurringTag,
    string SectionEmptySuffix,
    string SectionUnavailableSuffix,
    string SectionOmittedSuffix,
    string AmountUnavailableToken);

/// <summary>
/// Tabla estática de dos locales (es-DO/en) de <see cref="ChatContextLabelSet"/>
/// (F-015, D-4). <see cref="For"/> hace fallback a
/// <see cref="SupportedLocales.Default"/> para cualquier locale no reconocido,
/// mismo criterio de <c>EmbeddedPromptProvider</c>.
/// </summary>
public static class ChatContextLabels
{
    private static readonly ChatContextLabelSet _spanishDominican = new(
        TodayFormat: "Hoy: {0}. Montos en DOP salvo indicación.",
        MissingRatesFlag: "!! Algunos montos en otra moneda no se pudieron convertir a DOP y se omitieron; las cifras pueden estar incompletas.",
        CurrentMonthBaseLabel: "Mes actual",
        CurrentMonthSectionFormat: "## Mes actual: {0}",
        PriorMonthBaseLabel: "Mes anterior",
        PriorMonthSectionFormat: "## Mes anterior: {0}",
        ExpensesLabel: "Gastos",
        IncomesLabel: "Ingresos",
        NetBalanceLabel: "Balance neto",
        CurrentCategoriesBaseLabel: "Gastos por categoría (mes actual)",
        CurrentCategoriesSectionFormat: "## Gastos por categoría, {0}",
        PriorCategoriesBaseLabel: "Gastos por categoría (mes anterior)",
        PriorCategoriesSectionFormat: "## Gastos por categoría, {0} (top 10)",
        OverflowLineFormat: "Otras categorías ({0}): {1}",
        BudgetsBaseLabel: "Presupuestos",
        BudgetsSectionFormat: "## Presupuestos, {0}",
        BudgetLineFormat: "{0}: presupuesto {1}; gastado {2}; {3} %; estado {4}",
        BudgetIndeterminateFormat: "{0}: indeterminado (falta tasa de cambio)",
        BudgetStatusOk: "En orden",
        BudgetStatusNear: "Cerca del límite",
        BudgetStatusExceeded: "Excedido",
        GoalsBaseLabel: "Metas activas",
        GoalsSection: "## Metas activas",
        GoalLineFormat: "{0}: meta {1}; aportado {2}; restante {3}; {4} %; fecha objetivo {5}; aporte mensual requerido {6}",
        DebtsBaseLabel: "Deudas activas",
        DebtsSection: "## Deudas activas",
        DebtLineFormat: "{0}: saldo pendiente {1}; cuota {2}; cuotas pendientes {3}; próxima cuota {4}",
        NoNextDueDateToken: "sin cuotas pendientes",
        PaymentsBaseLabel: "Pagos próximos",
        PaymentsSectionFormat: "## Pagos próximos ({0} días)",
        PaymentLineFormat: "{0} — {1}: {2} {3}",
        DebtInstallmentTag: "(cuota de deuda)",
        RecurringTag: "(recurrente)",
        SectionEmptySuffix: "ninguna",
        SectionUnavailableSuffix: "no disponible (error al cargar)",
        SectionOmittedSuffix: "omitida (límite de tamaño)",
        AmountUnavailableToken: "monto no disponible");

    private static readonly ChatContextLabelSet _english = new(
        TodayFormat: "Today: {0}. Amounts in DOP unless noted.",
        MissingRatesFlag: "!! Some amounts in another currency could not be converted to DOP and were omitted; figures may be incomplete.",
        CurrentMonthBaseLabel: "Current month",
        CurrentMonthSectionFormat: "## Current month: {0}",
        PriorMonthBaseLabel: "Prior month",
        PriorMonthSectionFormat: "## Prior month: {0}",
        ExpensesLabel: "Expenses",
        IncomesLabel: "Income",
        NetBalanceLabel: "Net balance",
        CurrentCategoriesBaseLabel: "Expenses by category (current month)",
        CurrentCategoriesSectionFormat: "## Expenses by category, {0}",
        PriorCategoriesBaseLabel: "Expenses by category (prior month)",
        PriorCategoriesSectionFormat: "## Expenses by category, {0} (top 10)",
        OverflowLineFormat: "Other categories ({0}): {1}",
        BudgetsBaseLabel: "Budgets",
        BudgetsSectionFormat: "## Budgets, {0}",
        BudgetLineFormat: "{0}: budget {1}; spent {2}; {3} %; status {4}",
        BudgetIndeterminateFormat: "{0}: indeterminate (missing exchange rate)",
        BudgetStatusOk: "On track",
        BudgetStatusNear: "Near limit",
        BudgetStatusExceeded: "Exceeded",
        GoalsBaseLabel: "Active goals",
        GoalsSection: "## Active goals",
        GoalLineFormat: "{0}: target {1}; contributed {2}; remaining {3}; {4} %; target date {5}; required monthly contribution {6}",
        DebtsBaseLabel: "Active debts",
        DebtsSection: "## Active debts",
        DebtLineFormat: "{0}: pending balance {1}; installment {2}; pending installments {3}; next due {4}",
        NoNextDueDateToken: "no pending installments",
        PaymentsBaseLabel: "Upcoming payments",
        PaymentsSectionFormat: "## Upcoming payments ({0} days)",
        PaymentLineFormat: "{0} — {1}: {2} {3}",
        DebtInstallmentTag: "(debt installment)",
        RecurringTag: "(recurring)",
        SectionEmptySuffix: "none",
        SectionUnavailableSuffix: "unavailable (failed to load)",
        SectionOmittedSuffix: "omitted (size limit)",
        AmountUnavailableToken: "amount unavailable");

    /// <summary>Devuelve el conjunto de etiquetas para <paramref name="locale"/>, con fallback al locale por defecto.</summary>
    public static ChatContextLabelSet For(string? locale) =>
        string.Equals(locale, SupportedLocales.English, StringComparison.Ordinal) ? _english : _spanishDominican;
}
