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
/// <param name="ConvertedAmountDisplay">
/// Conversion display-time del monto a la otra moneda disponible
/// (ej. <c>"≈ 1,500.00 DOP"</c>). Lo popula el ViewModel consultando
/// <c>ICurrencyConverter</c> despues de cargar la pagina; <c>null</c>
/// cuando falla la conversion o cuando la moneda origen coincide con
/// la destino (no se muestra redundante). En Fase 5 siempre se calcula
/// a la otra moneda del par DOP/USD.
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
    string? PaymentMethodName,
    string? ConvertedAmountDisplay);
