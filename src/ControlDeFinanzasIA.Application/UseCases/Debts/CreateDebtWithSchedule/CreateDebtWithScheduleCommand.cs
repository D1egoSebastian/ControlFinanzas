using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Debts.CreateDebtWithSchedule;

/// <summary>
/// Command para registrar una nueva deuda con su plan de cuotas
/// completo (R-1, R-3).
/// </summary>
public sealed record CreateDebtWithScheduleCommand(
    string Description,
    decimal OriginalAmount,
    Currency Currency,
    int TotalInstallments,
    decimal InstallmentAmount,
    string Frequency,
    DateOnly StartDate,
    string? CounterpartyName = null,
    string? Note = null);
