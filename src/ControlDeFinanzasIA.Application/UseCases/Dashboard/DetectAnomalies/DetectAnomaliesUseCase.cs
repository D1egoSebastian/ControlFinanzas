using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Alerts;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.DetectAnomalies;

/// <summary>
/// Use case que detecta anomalias por gasto y reconcilia
/// <see cref="AnomalyAlert"/> para el mes evaluado, con <b>cero</b>
/// dependencia de <c>ILLMClient</c> (F-014, R-2, R-3, D-7). Idempotente:
/// dos cargas consecutivas del mismo mes dejan exactamente una alerta por
/// <c>TransactionId</c> marcado, y una alerta reconocida nunca se modifica
/// ni se vuelve a crear.
/// </summary>
public sealed class DetectAnomaliesUseCase
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAnomalyAlertRepository _anomalyAlertRepository;
    private readonly ICurrencyConverter _currencyConverter;
    private readonly IAnomalyMessageFormatter _messageFormatter;
    private readonly IClock _clock;
    private readonly ILogger<DetectAnomaliesUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public DetectAnomaliesUseCase(
        IExpenseRepository expenseRepository,
        ICategoryRepository categoryRepository,
        IAnomalyAlertRepository anomalyAlertRepository,
        ICurrencyConverter currencyConverter,
        IAnomalyMessageFormatter messageFormatter,
        IClock clock,
        ILogger<DetectAnomaliesUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(expenseRepository);
        ArgumentNullException.ThrowIfNull(categoryRepository);
        ArgumentNullException.ThrowIfNull(anomalyAlertRepository);
        ArgumentNullException.ThrowIfNull(currencyConverter);
        ArgumentNullException.ThrowIfNull(messageFormatter);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _anomalyAlertRepository = anomalyAlertRepository;
        _currencyConverter = currencyConverter;
        _messageFormatter = messageFormatter;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la deteccion y reconciliacion para el mes que contiene
    /// <paramref name="monthAnchor"/>.
    /// </summary>
    /// <param name="monthAnchor">Cualquier fecha dentro del mes evaluado.</param>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con un <see cref="AnomalyDetectionResult"/> en
    /// <see cref="Result.Value"/>. Nunca falla por razones de datos: un
    /// fallo de reconciliacion se registra y se continua con la vista
    /// previa a la reconciliacion.
    /// </returns>
    public async Task<Result> ExecuteAsync(DateOnly monthAnchor, CancellationToken ct = default)
    {
        DateOnly anchor = MonthPeriod.Anchor(monthAnchor);
        DateRange window = MonthPeriod.TrailingRange(anchor, AnomalyDetector.HistoryWindowMonths + 1);
        DateRange month = MonthPeriod.Range(anchor);

        IReadOnlyList<ExpenseAmountRow> rows = await _expenseRepository
            .GetAmountRowsAsync(window, ct)
            .ConfigureAwait(false);

        var history = new List<AnomalySample>();
        var candidates = new List<AnomalyCandidate>();
        var unconvertedExpenseIds = new HashSet<Guid>();
        var descriptionByExpenseId = new Dictionary<Guid, string>();
        bool hasMissingRates = false;

        foreach (ExpenseAmountRow row in rows)
        {
            bool isCandidate = row.Date >= anchor;

            var source = Money.Create(row.Amount, row.Currency);
            Result converted = await _currencyConverter.ConvertAsync(source, Currency.DOP, ct).ConfigureAwait(false);

            if (!converted.IsSuccess)
            {
                hasMissingRates = true;
                if (isCandidate)
                {
                    // "Cannot evaluate" is not "no longer qualifies" (Spec
                    // Reconciliation #2): the candidate is excluded from
                    // detection this load, but its existing alert (if any)
                    // must be kept, not removed.
                    unconvertedExpenseIds.Add(row.ExpenseId);
                }

                continue;
            }

            decimal amountDop = ((Money)converted.Value!).Amount;

            if (isCandidate)
            {
                candidates.Add(new AnomalyCandidate(row.ExpenseId, row.CategoryId, row.Date, amountDop));
                descriptionByExpenseId[row.ExpenseId] = row.Description;
            }
            else
            {
                history.Add(new AnomalySample(row.CategoryId, row.Date, amountDop));
            }
        }

        IReadOnlyList<AnomalyFinding> findings = AnomalyDetector.Detect(history, candidates);

        IReadOnlyList<AnomalyAlert> byMonth = await _anomalyAlertRepository
            .GetByMonthAsync(month, ct)
            .ConfigureAwait(false);
        IReadOnlyList<Guid> findingIds = [.. findings.Select(f => f.ExpenseId)];
        IReadOnlyList<AnomalyAlert> byFindingIds = await _anomalyAlertRepository
            .GetByTransactionIdsAsync(findingIds, ct)
            .ConfigureAwait(false);

        // Union by Id (not TransactionId): a single alert can be reachable via
        // both reads (e.g. it is dated in-month AND its TransactionId matches
        // a finding).
        Dictionary<Guid, AnomalyAlert> existingByTransactionId = new();
        foreach (AnomalyAlert alert in byMonth.Concat(byFindingIds))
        {
            existingByTransactionId[alert.TransactionId] = alert;
        }

        var categoryNames = (await _categoryRepository.GetAllAsync(ct).ConfigureAwait(false))
            .ToDictionary(c => c.Id, c => c.Name);

        var toAdd = new List<AnomalyAlert>();
        var toUpdate = new List<AnomalyAlert>();
        var handledTransactionIds = new HashSet<Guid>();

        foreach (AnomalyFinding finding in findings)
        {
            handledTransactionIds.Add(finding.ExpenseId);

            if (existingByTransactionId.TryGetValue(finding.ExpenseId, out AnomalyAlert? existingAlert))
            {
                if (existingAlert.IsAcknowledged)
                {
                    // Never modified, never recreated (S-3).
                    continue;
                }

                string categoryName = categoryNames.GetValueOrDefault(finding.CategoryId, string.Empty);
                string message = _messageFormatter.Format(new AnomalyMessageFacts(
                    categoryName, finding.AmountDop, finding.MedianDop, finding.RatioToMedian, finding.Severity));

                bool unchanged =
                    existingAlert.Date == finding.Date &&
                    existingAlert.Severity == finding.Severity &&
                    existingAlert.Message == message;

                if (!unchanged)
                {
                    existingAlert.Refresh(finding.Date, finding.Severity, message);
                    toUpdate.Add(existingAlert);
                }
            }
            else
            {
                string categoryName = categoryNames.GetValueOrDefault(finding.CategoryId, string.Empty);
                string message = _messageFormatter.Format(new AnomalyMessageFacts(
                    categoryName, finding.AmountDop, finding.MedianDop, finding.RatioToMedian, finding.Severity));

                toAdd.Add(AnomalyAlert.Create(
                    finding.ExpenseId, finding.CategoryId, finding.Date, finding.Severity, message, _clock.UtcNow));
            }
        }

        var toRemove = new List<Guid>();
        foreach (AnomalyAlert alert in byMonth)
        {
            if (alert.IsAcknowledged || handledTransactionIds.Contains(alert.TransactionId))
            {
                continue;
            }

            if (unconvertedExpenseIds.Contains(alert.TransactionId))
            {
                // Kept, not removed (Spec Reconciliation #2).
                continue;
            }

            // No longer qualifies (history changed) or the expense was
            // deleted -- either way it is not in `findings` anymore.
            toRemove.Add(alert.Id);
        }

        var plan = new AnomalyReconciliationPlan(toAdd, toUpdate, toRemove);
        bool reconciled = true;

        if (!plan.IsEmpty)
        {
            Result applyResult = await _anomalyAlertRepository.ApplyReconciliationAsync(plan, ct).ConfigureAwait(false);
            if (!applyResult.IsSuccess)
            {
                reconciled = false;
                _logger.LogWarning(
                    "Anomaly reconciliation failed for month {Month}: {ErrorCode} {ErrorMessage}",
                    anchor,
                    applyResult.Error?.Code,
                    applyResult.Error?.Message);
            }
        }

        List<AnomalyItemDto> items = BuildItems(
            findings, existingByTransactionId, toAdd, reconciled, categoryNames, descriptionByExpenseId);

        return Result.Success(new AnomalyDetectionResult(items, hasMissingRates));
    }

    /// <summary>
    /// Construye la lista de anomalias activas a devolver, en el orden del
    /// detector. Si la reconciliacion fallo, las anomalias completamente
    /// nuevas (<paramref name="toAdd"/>) se omiten -- nunca llegaron a la
    /// base de datos (D-7, paso 7: "continue with existing as the source of
    /// truth"). Una anomalia con alerta pre-existente sigue reportandose
    /// aunque el <c>SaveChangesAsync</c> del refresco haya fallado: el
    /// objeto en memoria ya refleja el intento (se reintentara en la
    /// proxima carga si la reconciliacion sigue fallando).
    /// </summary>
    private static List<AnomalyItemDto> BuildItems(
        IReadOnlyList<AnomalyFinding> findings,
        Dictionary<Guid, AnomalyAlert> existingByTransactionId,
        List<AnomalyAlert> toAdd,
        bool reconciled,
        Dictionary<Guid, string> categoryNames,
        Dictionary<Guid, string> descriptionByExpenseId)
    {
        var addedByTransactionId = toAdd.ToDictionary(a => a.TransactionId);
        List<AnomalyItemDto> items = [];

        foreach (AnomalyFinding finding in findings)
        {
            bool hasExisting = existingByTransactionId.TryGetValue(finding.ExpenseId, out AnomalyAlert? existingAlert);

            if (hasExisting && existingAlert!.IsAcknowledged)
            {
                continue;
            }

            Guid alertId;
            string message;

            if (hasExisting)
            {
                alertId = existingAlert!.Id;
                message = existingAlert.Message;
            }
            else if (reconciled && addedByTransactionId.TryGetValue(finding.ExpenseId, out AnomalyAlert? added))
            {
                alertId = added.Id;
                message = added.Message;
            }
            else
            {
                // Reconciliation failed and there was no pre-existing alert:
                // nothing was persisted for this finding, so it is not
                // reported this load.
                continue;
            }

            string categoryName = categoryNames.GetValueOrDefault(finding.CategoryId, string.Empty);
            string description = descriptionByExpenseId.GetValueOrDefault(finding.ExpenseId, string.Empty);

            items.Add(new AnomalyItemDto(
                alertId,
                finding.ExpenseId,
                finding.Date,
                categoryName,
                description,
                finding.AmountDop,
                finding.MedianDop,
                finding.RatioToMedian,
                finding.Severity,
                message));
        }

        return items;
    }
}
