using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Data transfer object that represents an <see cref="Domain.Transactions.Income"/> in the
/// application layer. It is the contract exposed to the UI and other
/// consumers outside of <c>Application</c>.
/// </summary>
/// <param name="Id">Unique identifier of the income.</param>
/// <param name="CategoryId">Optional category identifier; <c>null</c> when uncategorized.</param>
/// <param name="Date">Calendar date on which the income occurred.</param>
/// <param name="Amount">Monetary amount as a scalar value.</param>
/// <param name="Currency">Currency of the amount.</param>
/// <param name="Description">Short description of the income.</param>
/// <param name="Note">Optional longer note.</param>
/// <param name="Source">Optional origin of the income (e.g., "Salary").</param>
/// <param name="CreatedAt">UTC timestamp of creation.</param>
/// <param name="UpdatedAt">UTC timestamp of the last update.</param>
/// <param name="PaymentMethodId">
/// Identificador opcional del metodo de pago asociado. <c>null</c>
/// cuando el usuario no asigno uno.
/// </param>
/// <param name="PaymentMethodName">
/// Nombre legible del metodo de pago (ej. <c>"Efectivo"</c>). Populado
/// por el repositorio via Include; <c>null</c> cuando no hay metodo
/// o cuando el Include no se uso.
/// </param>
public sealed record IncomeDto(
    Guid Id,
    Guid? CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note,
    string? Source,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? PaymentMethodId,
    string? PaymentMethodName);
