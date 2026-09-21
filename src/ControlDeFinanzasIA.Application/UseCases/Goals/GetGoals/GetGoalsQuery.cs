using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Goals;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;

/// <summary>
/// Query que lista todas las metas de ahorro con su progreso y su plan
/// precalculados (D-7). Lee <see cref="IGoalRepository"/> y
/// <see cref="IGoalContributionRepository"/> directamente — no compone
/// ninguna otra query, mismo precedente que
/// <c>GetBudgetVsActualQuery</c>.
/// </summary>
public sealed class GetGoalsQuery
{
    private readonly IGoalRepository _goalRepository;
    private readonly IGoalContributionRepository _contributionRepository;
    private readonly IClock _clock;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetGoalsQuery(
        IGoalRepository goalRepository,
        IGoalContributionRepository contributionRepository,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(goalRepository);
        ArgumentNullException.ThrowIfNull(contributionRepository);
        ArgumentNullException.ThrowIfNull(clock);

        _goalRepository = goalRepository;
        _contributionRepository = contributionRepository;
        _clock = clock;
    }

    /// <summary>
    /// Ejecuta la query.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> con una <see cref="IReadOnlyList{T}"/> de
    /// <see cref="GoalPlanItem"/> en <see cref="Result.Value"/>,
    /// ordenada por <see cref="Goal.Status"/>, luego
    /// <see cref="Goal.TargetDate"/>, luego <see cref="Goal.Name"/>
    /// (ordinal). Un repositorio vacío produce una lista vacía sin
    /// consultar aportes.
    /// </returns>
    public async Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        DateOnly today = _clock.Today;

        IReadOnlyList<Goal> goals = await _goalRepository.GetAllAsync(ct).ConfigureAwait(false);
        if (goals.Count == 0)
        {
            return Result.Success((IReadOnlyList<GoalPlanItem>)Array.Empty<GoalPlanItem>());
        }

        var items = new List<GoalPlanItem>(goals.Count);
        foreach (Goal goal in goals)
        {
            IReadOnlyList<GoalContribution> contributions = await _contributionRepository
                .GetByGoalIdAsync(goal.Id, ct)
                .ConfigureAwait(false);

            GoalProgress progress = goal.CalculateProgress(contributions);
            SavingsPlan plan = SavingsPlanCalculator.Calculate(
                goal.TargetAmount.Amount, progress.Contributed.Amount, goal.TargetDate, today);

            items.Add(new GoalPlanItem(
                GoalId: goal.Id,
                Name: goal.Name,
                Currency: goal.Currency,
                TargetAmount: goal.TargetAmount.Amount,
                ContributedAmount: progress.Contributed.Amount,
                RemainingAmount: progress.Remaining.Amount,
                PercentComplete: progress.PercentComplete,
                TargetDate: goal.TargetDate,
                Status: goal.Status,
                MonthsRemaining: plan.MonthsRemaining,
                MonthlyContributionRequired: plan.MonthlyContributionRequired,
                AlreadyMet: plan.AlreadyMet,
                TargetDateInPast: plan.TargetDateInPast));
        }

        IReadOnlyList<GoalPlanItem> ordered = items
            .OrderBy(i => i.Status)
            .ThenBy(i => i.TargetDate)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        return Result.Success(ordered);
    }
}
