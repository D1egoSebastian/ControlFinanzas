using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Data transfer object that represents an <see cref="Expense"/> in the
/// application layer. It is the contract exposed to the UI and other
/// consumers outside of <c>Application</c>.
/// </summary>
public sealed record ExpenseDto(
    Guid Id,
    Guid CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note,
    DateTime CreatedAt,
    DateTime UpdatedAt);
