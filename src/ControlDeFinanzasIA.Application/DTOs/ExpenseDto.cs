using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Data transfer object that represents an <see cref="Expense"/> in the
/// application layer. It is the contract exposed to the UI and other
/// consumers outside of <c>Application</c>.
/// </summary>
/// <param name="PaymentMethodId">
/// Identificador opcional del metodo de pago asociado. <c>null</c>
/// cuando el usuario no asigno uno.
/// </param>
/// <param name="PaymentMethodName">
/// Nombre legible del metodo de pago (ej. <c>"Efectivo"</c>). Populado
/// por el repositorio via Include; <c>null</c> cuando no hay metodo
/// o cuando el Include no se uso.
/// </param>
public sealed record ExpenseDto(
    Guid Id,
    Guid CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? PaymentMethodId,
    string? PaymentMethodName);
