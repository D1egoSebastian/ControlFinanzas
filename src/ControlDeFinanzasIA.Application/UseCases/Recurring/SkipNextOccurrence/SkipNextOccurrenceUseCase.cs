using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;

public sealed class SkipNextOccurrenceUseCase
{
    private readonly IRecurringRuleRepository _repository;
    public SkipNextOccurrenceUseCase(IRecurringRuleRepository repository)
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

        int months = rule.Frequency switch { "Monthly" => 1, "Bimonthly" => 2, "Yearly" => 12, _ => 1 };
        rule.AdvanceAfterGeneration(rule.NextGenerationDate, months);
        return await _repository.UpdateAsync(rule, ct).ConfigureAwait(false);
    }
}
