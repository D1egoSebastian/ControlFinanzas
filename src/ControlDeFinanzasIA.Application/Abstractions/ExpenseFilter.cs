namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Contrato de transporte para filtrar y paginar gastos en
/// <see cref="IExpenseRepository.GetByFilterAsync"/>.
/// </summary>
/// <remarks>
/// Este record es "tonto": no valida sus propiedades. La validación
/// de <see cref="Page"/>, <see cref="PageSize"/>, coherencia de
/// fechas (<see cref="DateFrom"/> ≤ <see cref="DateTo"/>) y de
/// <see cref="TagIds"/> es responsabilidad del caller (F-005
/// <c>GetTransactionsQuery</c> y 4-008).
/// </remarks>
public sealed record ExpenseFilter
{
    /// <summary>
    /// Número de página solicitada (1-based). Valor por defecto: 1.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Cantidad de items por página. Valor por defecto: 50.
    /// </summary>
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Identificador de categoría para filtrar. <c>null</c> significa
    /// que no se aplica filtro por categoría.
    /// </summary>
    public Guid? CategoryId { get; init; }

    /// <summary>
    /// Fecha inicial inclusive del rango a filtrar. <c>null</c> significa
    /// que no se aplica cota inferior.
    /// </summary>
    public DateOnly? DateFrom { get; init; }

    /// <summary>
    /// Fecha final inclusive del rango a filtrar. <c>null</c> significa
    /// que no se aplica cota superior.
    /// </summary>
    public DateOnly? DateTo { get; init; }

    /// <summary>
    /// Texto a buscar dentro de la descripción (coincidencia parcial, sin distinguir mayúsculas
    /// ASCII). <c>null</c> significa que no se aplica búsqueda por texto.
    /// </summary>
    public string? Text { get; init; }

/// <summary>
/// Identificadores de tags para filtrar (semántica OR: la entidad
/// debe tener al menos uno). <c>null</c> o lista vacía significan
/// que no se aplica filtro por tags.
/// </summary>
public IReadOnlyList<Guid>? TagIds { get; init; }

    /// <summary>
    /// Identificadores de métodos de pago para filtrar (semántica OR:
    /// la entidad debe tener uno de los métodos listados). <c>null</c>
    /// o lista vacía significan que no se aplica filtro por método.
    /// </summary>
    /// <remarks>
    /// Fase 5 feature 5-006. Los gastos sin método (<c>PaymentMethodId</c>
    /// <c>null</c>) NO se incluyen cuando este filtro está activo: la
    /// query filtra a gastos que tengan uno de los métodos seleccionados.
    /// </remarks>
    public IReadOnlyList<Guid>? PaymentMethodIds { get; init; }
}
