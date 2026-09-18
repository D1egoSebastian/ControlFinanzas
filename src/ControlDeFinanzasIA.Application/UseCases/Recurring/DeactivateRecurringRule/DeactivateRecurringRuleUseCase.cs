using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateRecurringRule;

public sealed class DeactivateRecurringRuleUseCase
{
    private readonly IRecurringRuleRepository _repository;
    public DeactivateRecurringRuleUseCase(IRecurringRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        Domain.Recurring.RecurringRule? rule = await _repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (rule is null)
        {
            return Result.Failure("NotFound", $"Recurring rule {id} was not found.");
        }

        rule.Deactivate();
        return await _repository.UpdateAsync(rule, ct).ConfigureAwait(false);
    }
}
