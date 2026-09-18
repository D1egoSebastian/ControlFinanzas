using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteRecurringRule;

public sealed class DeleteRecurringRuleUseCase
{
    private readonly IRecurringRuleRepository _repository;
    public DeleteRecurringRuleUseCase(IRecurringRuleRepository repository)
    {
        _repository = repository;
    }

    public Task<Result> ExecuteAsync(Guid id, CancellationToken ct = default) => _repository.RemoveAsync(id, ct);
}
