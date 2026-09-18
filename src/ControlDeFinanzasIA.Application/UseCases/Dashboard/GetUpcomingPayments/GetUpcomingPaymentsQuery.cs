using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;

/// <summary>
/// Query que arma la tarjeta "Pr&#243;ximos pagos" del Dashboard (R-6): combina
/// cuotas de deuda pendientes (<see cref="IDebtPaymentRepository"/>) con las
/// pr&#243;ximas generaciones de reglas recurrentes activas
/// (<see cref="IRecurringRuleRepository"/>), dentro de un horizonte de
/// <see cref="DashboardDefaults.UpcomingHorizonDays"/> d&#237;as desde
/// <see cref="IClock.Today"/>, ordenadas por fecha y limitadas a
/// <see cref="DashboardDefaults.UpcomingMaxItems"/> elementos.
/// </summary>
/// <remarks>
/// A diferencia de <see cref="IDebtPaymentRepository.GetPendingByDateRangeAsync"/>
/// (filtro SQL: <c>DebtPayment</c> crece con deudas &#215; cuotas), las reglas
/// recurrentes se filtran en memoria sobre
/// <see cref="IRecurringRuleRepository.GetAllAsync"/> &#8212; la cardinalidad de
/// reglas del usuario es baja (D-3 del design.md), exactamente el mismo
/// patr&#243;n que <c>GetDebtsQuery</c> filtrando <see cref="Debt.Status"/> en
/// memoria. Esta tarjeta est&#225; anclada a <see cref="IClock.Today"/>, no al
/// mes navegado del Dashboard (R-1a, D-6): esta query no conoce el periodo
/// seleccionado, es el ViewModel quien decide cu&#225;ndo invocarla (una sola
/// vez, no en cada navegaci&#243;n de mes).
/// </remarks>
public sealed class GetUpcomingPaymentsQuery
{
    private readonly IDebtPaymentRepository _debtPaymentRepository;
    private readonly IDebtRepository _debtRepository;
    private readonly IRecurringRuleRepository _recurringRuleRepository;
    private readonly IClock _clock;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetUpcomingPaymentsQuery(
        IDebtPaymentRepository debtPaymentRepository,
        IDebtRepository debtRepository,
        IRecurringRuleRepository recurringRuleRepository,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(debtPaymentRepository);
        ArgumentNullException.ThrowIfNull(debtRepository);
        ArgumentNullException.ThrowIfNull(recurringRuleRepository);
        ArgumentNullException.ThrowIfNull(clock);

        _debtPaymentRepository = debtPaymentRepository;
        _debtRepository = debtRepository;
        _recurringRuleRepository = recurringRuleRepository;
        _clock = clock;
    }

    /// <summary>
    /// Ejecuta la query para el horizonte <c>[IClock.Today, IClock.Today + horizonDays]</c>,
    /// inclusivo en ambos extremos.
    /// </summary>
    /// <param name="horizonDays">Tama&#241;o del horizonte, en d&#237;as, desde hoy.</param>
    /// <param name="maxItems">M&#225;ximo de elementos devueltos, tras ordenar por fecha.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="IReadOnlyList{T}"/> de
    /// <see cref="UpcomingPaymentItem"/> en <see cref="Result.Value"/>,
    /// ordenado por <see cref="UpcomingPaymentItem.Date"/> ascendente y
    /// limitado a <paramref name="maxItems"/> elementos. Nunca falla por s&#237;
    /// misma: sin pagos pr&#243;ximos produce una lista vac&#237;a (R-7).
    /// </returns>
    public async Task<Result> ExecuteAsync(
        int horizonDays = DashboardDefaults.UpcomingHorizonDays,
        int maxItems = DashboardDefaults.UpcomingMaxItems,
        CancellationToken ct = default)
    {
        DateOnly today = _clock.Today;
        var horizon = DateRange.Create(today, today.AddDays(horizonDays));

        Task<IReadOnlyList<DebtPayment>> pendingPaymentsTask =
            _debtPaymentRepository.GetPendingByDateRangeAsync(horizon, ct);
        Task<IReadOnlyList<Debt>> debtsTask = _debtRepository.GetAllAsync(ct);
        Task<IReadOnlyList<RecurringRule>> rulesTask = _recurringRuleRepository.GetAllAsync(ct);

        await Task.WhenAll(pendingPaymentsTask, debtsTask, rulesTask).ConfigureAwait(false);

        var debtLabels = debtsTask.Result.ToDictionary(d => d.Id, d => d.Description);

        IEnumerable<UpcomingPaymentItem> debtItems = pendingPaymentsTask.Result.Select(payment =>
            new UpcomingPaymentItem(
                UpcomingPaymentKind.DebtInstallment,
                debtLabels.TryGetValue(payment.DebtId, out string? label) ? label : string.Empty,
                payment.ScheduledDate,
                payment.Amount));

        IEnumerable<UpcomingPaymentItem> ruleItems = rulesTask.Result
            .Where(rule => rule.Active && horizon.Contains(rule.NextGenerationDate))
            .Select(rule => new UpcomingPaymentItem(
                UpcomingPaymentKind.RecurringRule,
                rule.Name,
                rule.NextGenerationDate,
                rule.Amount));

        var items = debtItems
            .Concat(ruleItems)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Label, StringComparer.Ordinal)
            .Take(maxItems)
            .ToList();

        return Result.Success(items);
    }
}
