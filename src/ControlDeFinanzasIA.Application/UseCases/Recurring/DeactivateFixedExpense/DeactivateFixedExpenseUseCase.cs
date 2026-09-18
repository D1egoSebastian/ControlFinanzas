using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Recurring;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateFixedExpense;

public sealed class DeactivateFixedExpenseUseCase
{
    private readonly IRecurringRuleRepository _repository;
    public DeactivateFixedExpenseUseCase(IRecurringRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        RecurringRule? rule = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (rule is not FixedExpense)
        {
            return Result.Failure("NotFound", $"Fixed expense {id} was not found.");
        }

        rule.Deactivate();
        return await _repository.UpdateAsync(rule, ct).ConfigureAwait(false);
    }
}
