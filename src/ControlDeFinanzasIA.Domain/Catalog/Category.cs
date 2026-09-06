namespace ControlDeFinanzasIA.Domain.Catalog;

/// <summary>
/// Categoría de transacción. Las categorías del sistema (marcadas
/// con <c>IsSystem = true</c>) no se pueden eliminar.
/// </summary>
public sealed record Category
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public string? Icon { get; init; }
    public bool IsSystem { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
