using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;

namespace ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;

/// <summary>
/// Proyección de una meta con su progreso (<see cref="Goal.CalculateProgress"/>)
/// y su plan de ahorro (<see cref="SavingsPlanCalculator.Calculate"/>)
/// ya calculados (D-7). Único camino aritmético: esta query no
/// re-implementa ninguna suma ni fórmula, solo delega al dominio.
/// </summary>
public sealed record GoalPlanItem(
    Guid GoalId,
    string Name,
    Currency Currency,
    decimal TargetAmount,
    decimal ContributedAmount,
    decimal RemainingAmount,
    decimal PercentComplete,
    DateOnly TargetDate,
    GoalStatus Status,
    int MonthsRemaining,
    decimal MonthlyContributionRequired,
    bool AlreadyMet,
    bool TargetDateInPast);
