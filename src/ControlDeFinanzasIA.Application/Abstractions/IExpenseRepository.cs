using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="Expense"/>.
/// </summary>
public interface IExpenseRepository
{
    /// <summary>
    /// Añade un nuevo <see cref="Expense"/> al store.
    /// </summary>
    /// <param name="expense">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si falló (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(Expense expense, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="Expense"/> existente.
    /// </summary>
    /// <param name="expense">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(Expense expense, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="Expense"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación tuvo éxito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="Expense"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Expense?> GetByRecurringRuleAndPeriodAsync(Guid recurringRuleId, DateOnly period, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="Expense"/>. La colección retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene una página de <see cref="Expense"/> que cumplen con el
    /// filtro especificado, ordenados por <see cref="Expense.Date"/>
    /// descendente y con <see cref="Expense.CreatedAt"/> descendente
    /// como desempate.
    /// </summary>
    /// <param name="filter">Filtro de paginación y criterios de búsqueda.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="PagedResult{Expense}"/> con los items de la página
    /// solicitada y los metadatos de paginación.
    /// </returns>
    Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los totales de gasto por moneda dentro de
    /// <paramref name="period"/>, agregados en SQL (<c>GROUP BY</c>/<c>SUM</c>).
    /// No trae transacciones completas a memoria para sumarlas (R-3).
    /// </summary>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>Una fila por cada moneda presente en el periodo.</returns>
    Task<IReadOnlyList<CurrencyTotal>> GetPeriodTotalsAsync(DateRange period, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los totales de gasto por categoría x moneda dentro de
    /// <paramref name="period"/>, agregados en SQL (<c>GROUP BY</c>/<c>SUM</c>)
    /// con join a <c>Categories</c> para la etiqueta. Devuelve TODAS las
    /// categorías con gasto en el periodo; el ranking top-N y el bucket
    /// "Otras" se calculan en la capa Application, no aquí (D-2).
    /// </summary>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<CategoryCurrencyTotal>> GetCategoryTotalsAsync(DateRange period, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los totales de gasto por mes x moneda dentro de
    /// <paramref name="period"/>, agregados en SQL (<c>GROUP BY</c>/<c>SUM</c>).
    /// Usado por el gráfico de evolución temporal (R-5).
    /// </summary>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<MonthCurrencyTotal>> GetMonthlyTotalsAsync(DateRange period, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los totales de gasto por día x moneda dentro de
    /// <paramref name="period"/>, agregados en SQL (<c>GROUP BY</c>/<c>SUM</c>).
    /// Usado por la vista de calendario (R-5).
    /// </summary>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    Task<IReadOnlyList<DayCurrencyTotal>> GetDailyTotalsAsync(DateRange period, CancellationToken ct = default);

    /// <summary>
    /// Obtiene una fila por cada <see cref="Expense"/> dentro de
    /// <paramref name="period"/>, sin agregar (F-014, D-7). A diferencia de
    /// <see cref="GetPeriodTotalsAsync"/>/<see cref="GetCategoryTotalsAsync"/>,
    /// esta proyección no usa <c>Sum</c> en SQL, así que no necesita el
    /// workaround de cast a <c>double</c>: alimenta a
    /// <c>DetectAnomaliesUseCase</c>, que necesita el detalle por gasto para
    /// construir el historial y los candidatos de <c>AnomalyDetector</c>.
    /// </summary>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Filas ordenadas por <c>Date</c> ascendente, con <c>ExpenseId</c>
    /// ascendente como desempate.
    /// </returns>
    Task<IReadOnlyList<ExpenseAmountRow>> GetAmountRowsAsync(DateRange period, CancellationToken ct = default);
}
