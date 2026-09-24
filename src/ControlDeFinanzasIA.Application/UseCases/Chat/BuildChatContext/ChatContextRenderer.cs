using System.Globalization;
using System.Text.RegularExpressions;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;

/// <summary>
/// Resultado de <see cref="ChatContextRenderer.Render"/>: el texto final
/// listo para insertarse en el prompt del sistema (F-015, D-4) y la lista
/// de secciones que el recorte de presupuesto tuvo que omitir, en el orden
/// en que se omitieron.
/// </summary>
/// <param name="Text">Texto renderizado, garantizado <c>&lt;= <see cref="ChatContextLimits.MaxContextChars"/></c> por construcción.</param>
/// <param name="OmittedSections">Secciones reemplazadas por su marcador de "omitida" (S-5). Vacía si el contexto cupo sin recortar.</param>
public sealed record RenderedChatContext(string Text, IReadOnlyList<ChatContextSection> OmittedSections);

/// <summary>
/// Renderizador puro y estático (sin registro en DI, mismo estilo que
/// <see cref="Llm.AnomalyNarrationParser"/>) del <see cref="ChatContextSnapshot"/>
/// a texto listo para el prompt del sistema (F-015, R-3, D-4). Aplica
/// formato con la cultura real del locale (nunca separadores escritos a
/// mano), sanea nombres, y sostiene el presupuesto de
/// <see cref="ChatContextLimits.MaxContextChars"/> caracteres recortando
/// secciones completas en un orden fijo cuando hace falta (S-5). El resumen
/// del mes actual y su lista de categorías NUNCA se recortan (D-6).
/// </summary>
public static class ChatContextRenderer
{
    private const string _headerMarker = "=== FINANCIAL CONTEXT ===";
    private const string _footerMarker = "=== END FINANCIAL CONTEXT ===";
    private const string _isoDateFormat = "yyyy-MM-dd";

    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly Regex _controlCharsRegex = new(
        pattern: "[\\x00-\\x1F\\x7F]+",
        options: RegexOptions.None,
        matchTimeout: _regexTimeout);

    /// <summary>
    /// Orden fijo y probado (S-5) en el que se recortan secciones completas
    /// cuando el texto renderizado excede el presupuesto: pagos próximos →
    /// deudas → metas → presupuestos → categorías del mes anterior. El
    /// resumen del mes actual, la lista de categorías del mes actual, y el
    /// resumen del mes anterior NUNCA aparecen aquí (D-6).
    /// </summary>
    public static readonly ChatContextSection[] TrimOrder =
    [
        ChatContextSection.UpcomingPayments,
        ChatContextSection.Debts,
        ChatContextSection.Goals,
        ChatContextSection.Budgets,
        ChatContextSection.PriorMonthCategories,
    ];

    /// <summary>
    /// Renderiza <paramref name="snapshot"/> a texto para el prompt del
    /// sistema, formateado con la cultura de <paramref name="locale"/> y
    /// acotado a <see cref="ChatContextLimits.MaxContextChars"/> caracteres
    /// por construcción (D-4).
    /// </summary>
    public static RenderedChatContext Render(ChatContextSnapshot snapshot, string locale)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        string effectiveLocale = SupportedLocales.IsSupported(locale) ? locale : SupportedLocales.Default;
        var culture = CultureInfo.GetCultureInfo(effectiveLocale);
        ChatContextLabelSet labels = ChatContextLabels.For(effectiveLocale);

        string todayLine = string.Format(culture, labels.TodayFormat, snapshot.Today.ToString(_isoDateFormat, CultureInfo.InvariantCulture));
        string? flagLine = snapshot.HasMissingRates ? labels.MissingRatesFlag : null;

        DateOnly currentAnchor = snapshot.CurrentMonth?.Month ?? new DateOnly(snapshot.Today.Year, snapshot.Today.Month, 1);

        (ChatContextSection Section, string Text)[] sections =
        [
            (ChatContextSection.CurrentMonthSummary, RenderMonthSummary(
                snapshot.CurrentMonth, labels.CurrentMonthSectionFormat, labels.CurrentMonthBaseLabel, culture, labels)),
            (ChatContextSection.CurrentMonthCategories, RenderCategoryList(
                snapshot.CurrentCategories, labels.CurrentCategoriesSectionFormat, labels.CurrentCategoriesBaseLabel, culture, labels)),
            (ChatContextSection.PriorMonthSummary, RenderMonthSummary(
                snapshot.PriorMonth, labels.PriorMonthSectionFormat, labels.PriorMonthBaseLabel, culture, labels)),
            (ChatContextSection.PriorMonthCategories, RenderCategoryList(
                snapshot.PriorCategories, labels.PriorCategoriesSectionFormat, labels.PriorCategoriesBaseLabel, culture, labels)),
            (ChatContextSection.Budgets, RenderBudgets(snapshot.Budgets, currentAnchor, culture, labels)),
            (ChatContextSection.Goals, RenderGoals(snapshot.Goals, culture, labels)),
            (ChatContextSection.Debts, RenderDebts(snapshot.Debts, culture, labels)),
            (ChatContextSection.UpcomingPayments, RenderPayments(snapshot.UpcomingPayments, culture, labels)),
        ];

        var omitted = new List<ChatContextSection>();
        string text = Join(todayLine, flagLine, sections);

        int trimIndex = 0;
        while (text.Length > ChatContextLimits.MaxContextChars && trimIndex < TrimOrder.Length)
        {
            ChatContextSection sectionToTrim = TrimOrder[trimIndex];
            int index = Array.FindIndex(sections, s => s.Section == sectionToTrim);
            sections[index] = (sectionToTrim, OmittedLine(sectionToTrim, labels));
            omitted.Add(sectionToTrim);
            trimIndex++;
            text = Join(todayLine, flagLine, sections);
        }

        System.Diagnostics.Debug.Assert(
            text.Length <= ChatContextLimits.MaxContextChars,
            "Chat context exceeded the budget even after trimming every trimmable section.");

        return new RenderedChatContext(text, omitted);
    }

    /// <summary>
    /// Formatea un monto DOP con la cultura real de <paramref name="culture"/>
    /// (nunca separadores escritos a mano). Compartido con el panel de
    /// cifras exactas de la UI (Slice S5, R-10).
    /// </summary>
    public static string FormatDop(decimal amountDop, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return $"{amountDop.ToString("N2", culture)} DOP";
    }

    /// <summary>
    /// Sanea un nombre de dominio (categoría, meta, deuda, pago) antes de
    /// insertarlo en el contexto: colapsa caracteres de control/saltos de
    /// línea a un único espacio, convierte <c>[</c>/<c>]</c> a <c>(</c>/<c>)</c>,
    /// elimina cualquier racha inicial de <c>#</c>/<c>!</c> (para que un
    /// nombre de usuario no pueda inyectar un marcador de sección/bandera),
    /// recorta espacios, y trunca a <see cref="ChatContextLimits.MaxNameChars"/>
    /// caracteres con <c>…</c> si excede el tope.
    /// </summary>
    public static string SanitizeName(string raw)
    {
        string value = raw ?? string.Empty;
        value = _controlCharsRegex.Replace(value, " ");
        value = value.Replace('[', '(').Replace(']', ')');
        value = value.Trim();
        value = value.TrimStart('#', '!');
        value = value.Trim();

        if (value.Length > ChatContextLimits.MaxNameChars)
        {
            value = string.Concat(value.AsSpan(0, ChatContextLimits.MaxNameChars - 1), "…");
        }

        return value;
    }

    private static string Join(string todayLine, string? flagLine, (ChatContextSection Section, string Text)[] sections)
    {
        var lines = new List<string> { _headerMarker, todayLine };

        if (flagLine is not null)
        {
            lines.Add(flagLine);
        }

        foreach ((ChatContextSection _, string text) in sections)
        {
            lines.Add(text);
        }

        lines.Add(_footerMarker);

        return string.Join('\n', lines);
    }

    private static string OmittedLine(ChatContextSection section, ChatContextLabelSet labels) =>
        $"## {BaseLabelFor(section, labels)}: {labels.SectionOmittedSuffix}";

    private static string BaseLabelFor(ChatContextSection section, ChatContextLabelSet labels) => section switch
    {
        ChatContextSection.CurrentMonthSummary => labels.CurrentMonthBaseLabel,
        ChatContextSection.CurrentMonthCategories => labels.CurrentCategoriesBaseLabel,
        ChatContextSection.PriorMonthSummary => labels.PriorMonthBaseLabel,
        ChatContextSection.PriorMonthCategories => labels.PriorCategoriesBaseLabel,
        ChatContextSection.Budgets => labels.BudgetsBaseLabel,
        ChatContextSection.Goals => labels.GoalsBaseLabel,
        ChatContextSection.Debts => labels.DebtsBaseLabel,
        ChatContextSection.UpcomingPayments => labels.PaymentsBaseLabel,
        _ => string.Empty,
    };

    private static string MonthText(DateOnly anchor, CultureInfo culture) =>
        $"{anchor.ToString("MMMM yyyy", culture)} ({anchor.ToString("yyyy-MM", CultureInfo.InvariantCulture)})";

    private static string RenderMonthSummary(
        ChatMonthTotals? totals, string sectionFormat, string baseLabel, CultureInfo culture, ChatContextLabelSet labels)
    {
        if (totals is null)
        {
            return $"## {baseLabel}: {labels.SectionUnavailableSuffix}";
        }

        string header = string.Format(culture, sectionFormat, MonthText(totals.Month, culture));
        string expenses = $"{labels.ExpensesLabel}: {FormatDop(totals.ExpensesDop, culture)}";
        string incomes = $"{labels.IncomesLabel}: {FormatDop(totals.IncomesDop, culture)}";
        string net = $"{labels.NetBalanceLabel}: {FormatDop(totals.NetBalanceDop, culture)}";

        return string.Join('\n', header, expenses, incomes, net);
    }

    private static string RenderCategoryList(
        ChatCategoryList? list, string sectionFormat, string baseLabel, CultureInfo culture, ChatContextLabelSet labels)
    {
        if (list is null)
        {
            return $"## {baseLabel}: {labels.SectionUnavailableSuffix}";
        }

        if (list.Lines.Count == 0 && list.OverflowCount == 0)
        {
            return $"## {baseLabel}: {labels.SectionEmptySuffix}";
        }

        var lines = new List<string> { string.Format(culture, sectionFormat, MonthText(list.Month, culture)) };

        foreach (ChatCategoryLine line in list.Lines)
        {
            lines.Add($"{SanitizeName(line.Name)}: {FormatDop(line.AmountDop, culture)}");
        }

        if (list.OverflowCount > 0)
        {
            lines.Add(string.Format(culture, labels.OverflowLineFormat, list.OverflowCount, FormatDop(list.OverflowAmountDop, culture)));
        }

        return string.Join('\n', lines);
    }

    private static string RenderBudgets(
        IReadOnlyList<ChatBudgetLine>? budgets, DateOnly currentAnchor, CultureInfo culture, ChatContextLabelSet labels)
    {
        if (budgets is null)
        {
            return $"## {labels.BudgetsBaseLabel}: {labels.SectionUnavailableSuffix}";
        }

        if (budgets.Count == 0)
        {
            return $"## {labels.BudgetsBaseLabel}: {labels.SectionEmptySuffix}";
        }

        var lines = new List<string> { string.Format(culture, labels.BudgetsSectionFormat, MonthText(currentAnchor, culture)) };

        foreach (ChatBudgetLine budget in budgets)
        {
            string name = SanitizeName(budget.CategoryName);

            if (budget.Status is null || budget.BudgetDop is null || budget.ActualDop is null || budget.PercentConsumed is null)
            {
                lines.Add(string.Format(culture, labels.BudgetIndeterminateFormat, name));
                continue;
            }

            string status = BudgetStatusLabel(budget.Status.Value, labels);
            lines.Add(string.Format(
                culture,
                labels.BudgetLineFormat,
                name,
                FormatDop(budget.BudgetDop.Value, culture),
                FormatDop(budget.ActualDop.Value, culture),
                FormatPercent(budget.PercentConsumed.Value, culture),
                status));
        }

        return string.Join('\n', lines);
    }

    private static string RenderGoals(IReadOnlyList<ChatGoalLine>? goals, CultureInfo culture, ChatContextLabelSet labels)
    {
        if (goals is null)
        {
            return $"## {labels.GoalsBaseLabel}: {labels.SectionUnavailableSuffix}";
        }

        if (goals.Count == 0)
        {
            return $"## {labels.GoalsBaseLabel}: {labels.SectionEmptySuffix}";
        }

        var lines = new List<string> { labels.GoalsSection };

        foreach (ChatGoalLine goal in goals)
        {
            lines.Add(string.Format(
                culture,
                labels.GoalLineFormat,
                SanitizeName(goal.Name),
                FormatChatAmount(goal.Target, culture, labels),
                FormatChatAmount(goal.Contributed, culture, labels),
                FormatChatAmount(goal.Remaining, culture, labels),
                FormatPercent(goal.PercentComplete, culture),
                goal.TargetDate.ToString(_isoDateFormat, CultureInfo.InvariantCulture),
                FormatChatAmount(goal.MonthlyRequired, culture, labels)));
        }

        return string.Join('\n', lines);
    }

    private static string RenderDebts(IReadOnlyList<ChatDebtLine>? debts, CultureInfo culture, ChatContextLabelSet labels)
    {
        if (debts is null)
        {
            return $"## {labels.DebtsBaseLabel}: {labels.SectionUnavailableSuffix}";
        }

        if (debts.Count == 0)
        {
            return $"## {labels.DebtsBaseLabel}: {labels.SectionEmptySuffix}";
        }

        var lines = new List<string> { labels.DebtsSection };

        foreach (ChatDebtLine debt in debts)
        {
            string nextDue = debt.NextDueDate?.ToString(_isoDateFormat, CultureInfo.InvariantCulture) ?? labels.NoNextDueDateToken;
            lines.Add(string.Format(
                culture,
                labels.DebtLineFormat,
                SanitizeName(debt.Name),
                FormatChatAmount(debt.PendingBalance, culture, labels),
                FormatChatAmount(debt.Installment, culture, labels),
                debt.PendingInstallments,
                nextDue));
        }

        return string.Join('\n', lines);
    }

    private static string RenderPayments(IReadOnlyList<ChatPaymentLine>? payments, CultureInfo culture, ChatContextLabelSet labels)
    {
        if (payments is null)
        {
            return $"## {labels.PaymentsBaseLabel}: {labels.SectionUnavailableSuffix}";
        }

        if (payments.Count == 0)
        {
            return $"## {labels.PaymentsBaseLabel}: {labels.SectionEmptySuffix}";
        }

        var lines = new List<string> { string.Format(culture, labels.PaymentsSectionFormat, ChatContextLimits.UpcomingHorizonDays) };

        foreach (ChatPaymentLine payment in payments)
        {
            string kindTag = payment.Kind == UpcomingPaymentKind.DebtInstallment ? labels.DebtInstallmentTag : labels.RecurringTag;
            lines.Add(string.Format(
                culture,
                labels.PaymentLineFormat,
                payment.Date.ToString(_isoDateFormat, CultureInfo.InvariantCulture),
                SanitizeName(payment.Label),
                FormatChatAmount(payment.Amount, culture, labels),
                kindTag));
        }

        return string.Join('\n', lines);
    }

    private static string FormatChatAmount(ChatAmount amount, CultureInfo culture, ChatContextLabelSet labels)
    {
        if (amount.AmountDop is null)
        {
            return labels.AmountUnavailableToken;
        }

        string dop = FormatDop(amount.AmountDop.Value, culture);

        if (amount.OriginalCurrency == Currency.DOP)
        {
            return dop;
        }

        return $"{dop} ({amount.OriginalAmount.ToString("N2", culture)} {amount.OriginalCurrency})";
    }

    private static string FormatPercent(decimal value, CultureInfo culture) => $"{value.ToString("N2", culture)} %";

    private static string BudgetStatusLabel(BudgetStatus status, ChatContextLabelSet labels) => status switch
    {
        BudgetStatus.Ok => labels.BudgetStatusOk,
        BudgetStatus.Near => labels.BudgetStatusNear,
        BudgetStatus.Exceeded => labels.BudgetStatusExceeded,
        _ => labels.BudgetStatusOk,
    };
}
