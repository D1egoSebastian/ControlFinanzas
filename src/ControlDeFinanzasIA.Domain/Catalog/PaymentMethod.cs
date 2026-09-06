namespace ControlDeFinanzasIA.Domain.Catalog;

/// <summary>
/// Método de pago. Los métodos del sistema (efectivo, tarjeta) no
/// se pueden eliminar.
/// </summary>
public sealed record PaymentMethod
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
