using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Recurring;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteFixedExpense;

public sealed class DeleteFixedExpenseUseCase
{
    private readonly IRecurringRuleRepository _repository;
    public DeleteFixedExpenseUseCase(IRecurringRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        RecurringRule? rule = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        return rule is FixedExpense
            ? await _repository.RemoveAsync(id, ct).ConfigureAwait(false)
            : Result.Failure("NotFound", $"Fixed expense {id} was not found.");
    }
}
