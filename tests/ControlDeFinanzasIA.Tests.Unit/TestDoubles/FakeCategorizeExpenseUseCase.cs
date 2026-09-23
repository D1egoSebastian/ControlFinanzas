using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Expenses.CategorizeExpense;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="CategorizeExpenseUseCase"/>, used by
/// <c>ExpensesViewModel</c> suggestion tests (F-013, slice 6) to control
/// exactly when a suggestion completes, is cancelled, or throws — without
/// spinning up an HTTP fake. <see cref="CategorizeExpenseUseCase.ExecuteAsync"/>
/// is <see langword="virtual"/> for exactly this reason; the base
/// constructor's collaborators are never exercised because
/// <see cref="ExecuteAsync"/> is fully overridden here.
/// </summary>
public sealed class FakeCategorizeExpenseUseCase : CategorizeExpenseUseCase
{
    /// <summary>Creates a new <see cref="FakeCategorizeExpenseUseCase"/> wired with inert collaborators.</summary>
    public FakeCategorizeExpenseUseCase()
        : base(
            new FakeSettingsStore(),
            new FakeLlmClientFactory(),
            new StrictReadOnlyCategoryRepository(),
            new FakePromptProvider(),
            NullLogger<CategorizeExpenseUseCase>.Instance)
    {
    }

    /// <summary>
    /// When set, <see cref="ExecuteAsync"/> delegates to this handler
    /// instead of returning <see cref="DefaultResult"/>. Lets a test
    /// simulate latency, cancellation, or an arbitrary <see cref="Result"/>.
    /// </summary>
    public Func<CategorizeExpenseCommand, CancellationToken, Task<Result>>? Handler { get; set; }

    /// <summary>Gets or sets the result returned by <see cref="ExecuteAsync"/> when <see cref="Handler"/> is <c>null</c>.</summary>
    public Result DefaultResult { get; set; } =
        Result.Success(new CategorySuggestionDto(null, null, false, string.Empty));

    /// <summary>Every <see cref="CategorizeExpenseCommand"/> passed to <see cref="ExecuteAsync"/>, in call order.</summary>
    public List<CategorizeExpenseCommand> Calls { get; } = [];

    /// <inheritdoc />
    public override async Task<Result> ExecuteAsync(CategorizeExpenseCommand command, CancellationToken ct = default)
    {
        Calls.Add(command);

        if (Handler is not null)
        {
            return await Handler(command, ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();

        return DefaultResult;
    }
}
