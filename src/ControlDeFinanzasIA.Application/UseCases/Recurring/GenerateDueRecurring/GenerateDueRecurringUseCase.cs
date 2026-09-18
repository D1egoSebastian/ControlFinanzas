using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;

public sealed record RecurringGenerationPreview(
    Guid RuleId, string RuleName, DateOnly OccurrenceDate, DateOnly Period,
    decimal Amount, ControlDeFinanzasIA.Domain.Enums.Currency Currency, bool IsSubscription);

public sealed class GenerateDueRecurringUseCase
{
    private readonly IRecurringRuleRepository _rules;
    private readonly IExpenseRepository _expenses;
    private readonly IClock _clock;
    private readonly ILogger<GenerateDueRecurringUseCase> _logger;
    private readonly RecurringOccurrenceCalculator _calculator = new();

    public GenerateDueRecurringUseCase(IRecurringRuleRepository rules, IExpenseRepository expenses, IClock clock, ILogger<GenerateDueRecurringUseCase> logger)
    {
        (_rules, _expenses, _clock, _logger) = (rules, expenses, clock, logger);
    }

    public Task<Result> PreviewAsync(CancellationToken ct = default) => PreviewAsync(new DateOnly(_clock.Today.Year, _clock.Today.Month, 1), ct);

    public async Task<Result> PreviewAsync(DateOnly period, CancellationToken ct = default)
    {
        DateOnly start = new(period.Year, period.Month, 1);
        DateOnly end = start.AddMonths(1).AddDays(-1);
        var previews = new List<RecurringGenerationPreview>();
        foreach (RecurringRule rule in await _rules.GetAllAsync(ct).ConfigureAwait(false))
        {
            foreach (DateOnly date in _calculator.CalculateOccurrences(rule, start, end))
            {
                if (date < rule.NextGenerationDate)
                {
                    continue;
                }

                if (await _expenses.GetByRecurringRuleAndPeriodAsync(rule.Id, start, ct).ConfigureAwait(false) is not null)
                {
                    continue;
                }

                previews.Add(new(rule.Id, rule.Name, date, start, rule.Amount.Amount, rule.Currency, rule is Subscription));
            }
        }
        return Result.Success((IReadOnlyList<RecurringGenerationPreview>)previews);
    }

    public Task<Result> ExecuteAsync(CancellationToken ct = default) => ExecuteAsync(new DateOnly(_clock.Today.Year, _clock.Today.Month, 1), ct);

    public async Task<Result> ExecuteAsync(DateOnly period, CancellationToken ct = default)
    {
        DateOnly start = new(period.Year, period.Month, 1);
        DateOnly end = start.AddMonths(1).AddDays(-1);
        int created = 0;
        foreach (RecurringRule rule in await _rules.GetAllAsync(ct).ConfigureAwait(false))
        {
            foreach (DateOnly date in _calculator.CalculateOccurrences(rule, start, end))
            {
                if (date < rule.NextGenerationDate || await _expenses.GetByRecurringRuleAndPeriodAsync(rule.Id, start, ct).ConfigureAwait(false) is not null)
                {
                    continue;
                }

                try
                {
                    var expense = Expense.CreateGenerated(rule.CategoryId, date, Money.Create(rule.Amount.Amount, rule.Currency), rule.Name,
                        "Generated from recurring rule", _clock.UtcNow, rule.PaymentMethodId, rule.Id, start);
                    Result add = await _expenses.AddAsync(expense, ct).ConfigureAwait(false);
                    if (!add.IsSuccess)
                    {
                        return add;
                    }

                    int months = rule.Frequency switch { "Monthly" => 1, "Bimonthly" => 2, "Yearly" => 12, _ => 1 };
                    rule.AdvanceAfterGeneration(date, months);
                    Result update = await _rules.UpdateAsync(rule, ct).ConfigureAwait(false);
                    if (!update.IsSuccess)
                    {
                        return update;
                    }

                    created++;
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning(ex, "Unable to generate recurring expense for rule {RuleId}.", rule.Id);
                    return Result.Failure("DomainInvariant", ex.Message);
                }
            }
        }
        return Result.Success(created);
    }
}
