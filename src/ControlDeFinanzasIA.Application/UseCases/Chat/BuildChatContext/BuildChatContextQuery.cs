using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Chat.BuildChatContext;

/// <summary>
/// Query compuesta que arma el <see cref="ChatContextSnapshot"/> enviado
/// al modelo de chat (F-015, R-2, D-1): compone SEIS queries ya
/// existentes (resumen de mes, desglose de categorías, presupuesto vs.
/// real, metas, deudas + calendario de cuotas, pagos próximos), nunca
/// accede a un repositorio directamente. Las secciones corren
/// SECUENCIALMENTE (nunca <c>Task.WhenAll</c>), mismo razonamiento de
/// <c>GetDashboardInsightsQuery</c> (D-10 de F-014): el <c>AppDbContext</c>
/// compartido de vida "scoped" es efectivamente de proceso completo para
/// los ViewModels transitorios de esta app.
/// </summary>
/// <remarks>
/// Cada sección tiene su propio <c>try/catch</c>: una falla (excepción o
/// <see cref="Result"/> con <c>IsSuccess = false</c>) deja esa sección en
/// <see langword="null"/> ("no disponible") sin afectar a las demás. Una
/// <see cref="OperationCanceledException"/> causada por
/// <paramref name="ct"/> nunca se atrapa: el guard
/// <c>when (!ct.IsCancellationRequested)</c> de cada <c>catch</c> deja que
/// se propague sin cambios (Stop / navegar fuera del chat).
/// </remarks>
public sealed class BuildChatContextQuery
{
    private readonly GetMonthSummaryQuery _monthSummary;
    private readonly GetCategoryBreakdownQuery _categoryBreakdown;
    private readonly GetBudgetVsActualQuery _budgetVsActual;
    private readonly GetGoalsQuery _goals;
    private readonly GetDebtsQuery _debts;
    private readonly GetDebtScheduleQuery _debtSchedule;
    private readonly GetUpcomingPaymentsQuery _upcomingPayments;
    private readonly ICurrencyConverter _converter;
    private readonly IClock _clock;
    private readonly ILogger<BuildChatContextQuery> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public BuildChatContextQuery(
        GetMonthSummaryQuery monthSummary,
        GetCategoryBreakdownQuery categoryBreakdown,
        GetBudgetVsActualQuery budgetVsActual,
        GetGoalsQuery goals,
        GetDebtsQuery debts,
        GetDebtScheduleQuery debtSchedule,
        GetUpcomingPaymentsQuery upcomingPayments,
        ICurrencyConverter converter,
        IClock clock,
        ILogger<BuildChatContextQuery> logger)
    {
        ArgumentNullException.ThrowIfNull(monthSummary);
        ArgumentNullException.ThrowIfNull(categoryBreakdown);
        ArgumentNullException.ThrowIfNull(budgetVsActual);
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(debts);
        ArgumentNullException.ThrowIfNull(debtSchedule);
        ArgumentNullException.ThrowIfNull(upcomingPayments);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _monthSummary = monthSummary;
        _categoryBreakdown = categoryBreakdown;
        _budgetVsActual = budgetVsActual;
        _goals = goals;
        _debts = debts;
        _debtSchedule = debtSchedule;
        _upcomingPayments = upcomingPayments;
        _converter = converter;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta las siete secciones y arma el <see cref="ChatContextSnapshot"/>.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional (Stop / navegar fuera del chat).</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> con un
    /// <see cref="ChatContextSnapshot"/> en <see cref="Result.Value"/>.
    /// Nunca <see cref="Result.Failure(string, string)"/>: una sección que
    /// falla queda <see langword="null"/> en el snapshot en vez de
    /// propagar el error (D-1).
    /// </returns>
    public async Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        DateOnly today = _clock.Today;
        DateOnly priorAnchor = MonthPeriod.Previous(today);
        bool hasMissingRates = false;

        (ChatMonthTotals? currentMonth, ChatMonthTotals? priorMonth, bool summaryMissing) =
            await BuildMonthTotalsAsync(today, priorAnchor, ct).ConfigureAwait(false);
        hasMissingRates |= summaryMissing;

        (ChatCategoryList? currentCategories, bool currentCategoriesMissing) =
            await BuildCategoryListAsync(today, ChatContextSection.CurrentMonthCategories, ChatContextLimits.CurrentMonthCategories, ct)
                .ConfigureAwait(false);
        hasMissingRates |= currentCategoriesMissing;

        (ChatCategoryList? priorCategories, bool priorCategoriesMissing) =
            await BuildCategoryListAsync(priorAnchor, ChatContextSection.PriorMonthCategories, ChatContextLimits.PriorMonthCategories, ct)
                .ConfigureAwait(false);
        hasMissingRates |= priorCategoriesMissing;

        (IReadOnlyList<ChatBudgetLine>? budgets, bool budgetsMissing) =
            await BuildBudgetsAsync(today, ct).ConfigureAwait(false);
        hasMissingRates |= budgetsMissing;

        (IReadOnlyList<ChatGoalLine>? goals, bool goalsMissing) =
            await BuildGoalsAsync(ct).ConfigureAwait(false);
        hasMissingRates |= goalsMissing;

        (IReadOnlyList<ChatDebtLine>? debts, bool debtsMissing) =
            await BuildDebtsAsync(ct).ConfigureAwait(false);
        hasMissingRates |= debtsMissing;

        (IReadOnlyList<ChatPaymentLine>? upcomingPayments, bool paymentsMissing) =
            await BuildUpcomingPaymentsAsync(ct).ConfigureAwait(false);
        hasMissingRates |= paymentsMissing;

        var snapshot = new ChatContextSnapshot(
            today,
            currentMonth,
            priorMonth,
            currentCategories,
            priorCategories,
            budgets,
            goals,
            debts,
            upcomingPayments,
            hasMissingRates);

        return Result.Success(snapshot);
    }

    /// <summary>
    /// Sección 1: totales del mes actual y del anterior (D-1: una sola
    /// llamada, <see cref="GetMonthSummaryQuery"/> ya trae ambos periodos).
    /// </summary>
    private async Task<(ChatMonthTotals? Current, ChatMonthTotals? Prior, bool HasMissingRates)> BuildMonthTotalsAsync(
        DateOnly today, DateOnly priorAnchor, CancellationToken ct)
    {
        try
        {
            Result result = await _monthSummary.ExecuteAsync(today, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                LogSectionFailure(ChatContextSection.CurrentMonthSummary, result);
                return (null, null, false);
            }

            var summary = (MonthSummaryResult)result.Value!;
            var current = new ChatMonthTotals(
                MonthPeriod.Anchor(today), summary.ExpensesDop, summary.IncomesDop, summary.NetBalanceDop);
            decimal priorNet = summary.PreviousIncomesDop - summary.PreviousExpensesDop;
            var prior = new ChatMonthTotals(
                priorAnchor, summary.PreviousExpensesDop, summary.PreviousIncomesDop, priorNet);

            return (current, prior, summary.HasMissingRates);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chat context section failed. Section={Section}", ChatContextSection.CurrentMonthSummary);
            return (null, null, false);
        }
    }

    /// <summary>
    /// Secciones 2 y 3: desglose de categorías de un mes, pedido SIN
    /// folding (<see cref="ChatContextLimits.UnfoldedTopCount"/>) y
    /// recortado por el builder mismo al tope de <paramref name="cap"/>
    /// nombradas más una línea de desbordamiento (D-1, reconciliación #1
    /// del spec).
    /// </summary>
    private async Task<(ChatCategoryList? List, bool HasMissingRates)> BuildCategoryListAsync(
        DateOnly monthAnchor, ChatContextSection section, int cap, CancellationToken ct)
    {
        try
        {
            Result result = await _categoryBreakdown
                .ExecuteAsync(monthAnchor, ChatContextLimits.UnfoldedTopCount, ct)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                LogSectionFailure(section, result);
                return (null, false);
            }

            var breakdown = (CategoryBreakdownResult)result.Value!;
            var named = breakdown.Slices.Where(s => s.CategoryId is not null).ToList();

            var lines = named
                .Take(cap)
                .Select(s => new ChatCategoryLine(s.Label, s.AmountDop))
                .ToList();

            int overflowCount = Math.Max(0, named.Count - cap);
            decimal overflowAmountDop = overflowCount > 0 ? named.Skip(cap).Sum(s => s.AmountDop) : 0m;

            var list = new ChatCategoryList(
                MonthPeriod.Anchor(monthAnchor), lines, overflowCount, overflowAmountDop, breakdown.TotalDop);

            return (list, breakdown.HasMissingRates);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chat context section failed. Section={Section}", section);
            return (null, false);
        }
    }

    /// <summary>Sección 4: presupuesto vs. real del mes actual, tomando las líneas en el orden ya producido por la query.</summary>
    private async Task<(IReadOnlyList<ChatBudgetLine>? Budgets, bool HasMissingRates)> BuildBudgetsAsync(
        DateOnly today, CancellationToken ct)
    {
        try
        {
            Result result = await _budgetVsActual.ExecuteAsync(today, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                LogSectionFailure(ChatContextSection.Budgets, result);
                return (null, false);
            }

            var budgetVsActual = (BudgetVsActualResult)result.Value!;
            IReadOnlyList<ChatBudgetLine> lines = budgetVsActual.Lines
                .Take(ChatContextLimits.Budgets)
                .Select(l => new ChatBudgetLine(l.CategoryName, l.BudgetDop, l.ActualDop, l.PercentConsumed, l.Status))
                .ToList();

            return (lines, budgetVsActual.HasMissingRates);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chat context section failed. Section={Section}", ChatContextSection.Budgets);
            return (null, false);
        }
    }

    /// <summary>
    /// Sección 5: metas activas, en el orden que ya produce la query,
    /// recortadas al tope tras filtrar por <see cref="GoalStatus.Active"/>.
    /// Cada monto pasa por <see cref="ConvertToChatAmountAsync"/> (D-3).
    /// </summary>
    private async Task<(IReadOnlyList<ChatGoalLine>? Goals, bool HasMissingRates)> BuildGoalsAsync(CancellationToken ct)
    {
        try
        {
            Result result = await _goals.ExecuteAsync(ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                LogSectionFailure(ChatContextSection.Goals, result);
                return (null, false);
            }

            var items = (IReadOnlyList<GoalPlanItem>)result.Value!;
            bool hasMissingRates = false;
            var lines = new List<ChatGoalLine>();

            foreach (GoalPlanItem item in items.Where(g => g.Status == GoalStatus.Active).Take(ChatContextLimits.Goals))
            {
                ChatAmount target = await ConvertToChatAmountAsync(Money.Create(item.TargetAmount, item.Currency), ct).ConfigureAwait(false);
                ChatAmount contributed = await ConvertToChatAmountAsync(Money.Create(item.ContributedAmount, item.Currency), ct).ConfigureAwait(false);
                ChatAmount remaining = await ConvertToChatAmountAsync(Money.Create(item.RemainingAmount, item.Currency), ct).ConfigureAwait(false);
                ChatAmount monthlyRequired = await ConvertToChatAmountAsync(Money.Create(item.MonthlyContributionRequired, item.Currency), ct).ConfigureAwait(false);

                hasMissingRates |= target.AmountDop is null || contributed.AmountDop is null
                    || remaining.AmountDop is null || monthlyRequired.AmountDop is null;

                lines.Add(new ChatGoalLine(item.Name, target, contributed, remaining, item.PercentComplete, item.TargetDate, monthlyRequired));
            }

            return (lines, hasMissingRates);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chat context section failed. Section={Section}", ChatContextSection.Goals);
            return (null, false);
        }
    }

    /// <summary>
    /// Sección 6: deudas activas, ordenadas por descripción (ordinal),
    /// recortadas al tope ANTES de leer el calendario de cuotas de cada
    /// una (D-1: la I/O por deuda queda acotada a lo sumo al tope). Una
    /// deuda cuyo calendario ya no se encuentra (carrera entre el listado
    /// y la lectura, <c>GetDebtScheduleQuery</c> devuelve
    /// <c>Result.Failure("NotFound", …)</c>) se omite individualmente sin
    /// invalidar la sección completa.
    /// </summary>
    private async Task<(IReadOnlyList<ChatDebtLine>? Debts, bool HasMissingRates)> BuildDebtsAsync(CancellationToken ct)
    {
        try
        {
            Result result = await _debts.ExecuteAsync(DebtStatus.Active, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                LogSectionFailure(ChatContextSection.Debts, result);
                return (null, false);
            }

            var debts = (IReadOnlyList<Debt>)result.Value!;
            IEnumerable<Debt> ordered = debts.OrderBy(d => d.Description, StringComparer.Ordinal).Take(ChatContextLimits.Debts);

            bool hasMissingRates = false;
            var lines = new List<ChatDebtLine>();

            foreach (Debt debt in ordered)
            {
                Result scheduleResult = await _debtSchedule.ExecuteAsync(debt.Id, ct).ConfigureAwait(false);
                if (!scheduleResult.IsSuccess)
                {
                    continue;
                }

                var payments = (IReadOnlyList<DebtPayment>)scheduleResult.Value!;
                Money pendingBalance = debt.CalculatePendingBalance(payments);

                ChatAmount pendingBalanceAmount = await ConvertToChatAmountAsync(pendingBalance, ct).ConfigureAwait(false);
                ChatAmount installmentAmount = await ConvertToChatAmountAsync(debt.InstallmentAmount, ct).ConfigureAwait(false);
                hasMissingRates |= pendingBalanceAmount.AmountDop is null || installmentAmount.AmountDop is null;

                int pendingInstallments = payments.Count(p => p.Status == DebtPaymentStatus.Pending);
                DateOnly? nextDueDate = payments
                    .Where(p => p.Status == DebtPaymentStatus.Pending)
                    .Select(p => (DateOnly?)p.ScheduledDate)
                    .OrderBy(d => d)
                    .FirstOrDefault();

                lines.Add(new ChatDebtLine(debt.Description, pendingBalanceAmount, installmentAmount, pendingInstallments, nextDueDate));
            }

            return (lines, hasMissingRates);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chat context section failed. Section={Section}", ChatContextSection.Debts);
            return (null, false);
        }
    }

    /// <summary>Sección 7: pagos próximos dentro del horizonte configurado, cada monto pasado por <see cref="ConvertToChatAmountAsync"/>.</summary>
    private async Task<(IReadOnlyList<ChatPaymentLine>? Payments, bool HasMissingRates)> BuildUpcomingPaymentsAsync(CancellationToken ct)
    {
        try
        {
            Result result = await _upcomingPayments
                .ExecuteAsync(ChatContextLimits.UpcomingHorizonDays, ChatContextLimits.UpcomingPayments, ct)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                LogSectionFailure(ChatContextSection.UpcomingPayments, result);
                return (null, false);
            }

            var items = (IReadOnlyList<UpcomingPaymentItem>)result.Value!;
            bool hasMissingRates = false;
            var lines = new List<ChatPaymentLine>();

            foreach (UpcomingPaymentItem item in items)
            {
                ChatAmount amount = await ConvertToChatAmountAsync(item.Amount, ct).ConfigureAwait(false);
                hasMissingRates |= amount.AmountDop is null;
                lines.Add(new ChatPaymentLine(item.Date, item.Label, item.Kind, amount));
            }

            return (lines, hasMissingRates);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chat context section failed. Section={Section}", ChatContextSection.UpcomingPayments);
            return (null, false);
        }
    }

    /// <summary>
    /// Convierte un monto por-ítem a DOP (D-3). A diferencia de las
    /// secciones agregadas, NO tiene su propio <c>try/catch</c>: una
    /// <see cref="OperationCanceledException"/> del <paramref name="ct"/>
    /// del caller debe propagar sin que ninguna sección la trate como una
    /// falla de conversión. <see cref="ICurrencyConverter.ConvertAsync"/>
    /// nunca lanza por una tasa de cambio no disponible (retorna
    /// <see cref="Result.Failure(string, string)"/>), así que ninguna
    /// suma queda envenenada por un fallo parcial (D-2, D-3).
    /// </summary>
    private async Task<ChatAmount> ConvertToChatAmountAsync(Money source, CancellationToken ct)
    {
        Result converted = await _converter.ConvertAsync(source, Currency.DOP, ct).ConfigureAwait(false);
        decimal? amountDop = converted.IsSuccess ? ((Money)converted.Value!).Amount : null;
        return new ChatAmount(amountDop, source.Amount, source.Currency);
    }

    private void LogSectionFailure(ChatContextSection section, Result result) =>
        _logger.LogWarning(
            "Chat context section failed. Section={Section} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
            section,
            result.Error?.Code,
            result.Error?.Message);
}
