using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación concreta de <see cref="IIncomeRepository"/> basada en
/// Entity Framework Core y el <see cref="AppDbContext"/> del proyecto.
/// </summary>
public sealed class IncomeRepository : IIncomeRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="IncomeRepository"/>.
    /// </summary>
    /// <param name="db">Contexto de EF Core utilizado para persistir los ingresos.</param>
    public IncomeRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Añade un nuevo <see cref="Income"/> a la base de datos.
    /// </summary>
    /// <remarks>
    /// Valida que la categoría referenciada por <see cref="Income.CategoryId"/>
    /// exista antes de persistir, pero solo cuando <see cref="Income.CategoryId"/>
    /// no es <c>null</c>. Si no existe, devuelve
    /// <c>Result.Failure("CategoryNotFound", ...)</c>.
    /// </remarks>
    /// <param name="income">Ingreso a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se persistió; de lo contrario,
    /// un error con código <c>CategoryNotFound</c>.
    /// </returns>
    public async Task<Result> AddAsync(Income income, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(income);

        if (income.CategoryId is { } catId)
        {
            Category? category = await _db.Categories.FindAsync(new object?[] { catId }, ct);
            if (category is null)
            {
                return Result.Failure(
                    "CategoryNotFound",
                    $"La categoría {catId} no existe.");
            }
        }

        _db.Incomes.Add(income);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Actualiza un <see cref="Income"/> existente.
    /// </summary>
    /// <remarks>
    /// Si el ingreso no existe, devuelve <c>Result.Failure("NotFound", ...)</c>.
    /// No re-valida la FK a <c>Categories</c>; el use case caller es
    /// responsable de la coherencia cross-aggregate.
    /// </remarks>
    /// <param name="income">Ingreso con los valores actualizados.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se actualizó; de lo contrario,
    /// un error con código <c>NotFound</c>.
    /// </returns>
    public async Task<Result> UpdateAsync(Income income, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(income);

        Income? existing = await _db.Incomes.FindAsync(new object?[] { income.Id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Ingreso {income.Id} no encontrado.");
        }

        _db.Entry(existing).CurrentValues.SetValues(income);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Elimina un <see cref="Income"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Realiza un hard delete: la fila se elimina físicamente de la tabla
    /// <c>Incomes</c>. Si el ingreso no existe, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>.
    /// </remarks>
    /// <param name="id">Identificador del ingreso a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se eliminó; de lo contrario,
    /// un error con código <c>NotFound</c>.
    /// </returns>
    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(
                "NotFound",
                $"Ingreso {id} no encontrado.");
        }

        Income? existing = await _db.Incomes.FindAsync(new object?[] { id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Ingreso {id} no encontrado.");
        }

        _db.Incomes.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Obtiene un <see cref="Income"/> por su identificador.
    /// </summary>
    /// <param name="id">Identificador del ingreso.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// El ingreso encontrado, o <see langword="null"/> si no existe.
    /// </returns>
    public async Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _db.Incomes
            .Include(i => i.PaymentMethod)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    /// <summary>
    /// Obtiene todos los <see cref="Income"/> ordenados por fecha
    /// descendente, con <see cref="Income.CreatedAt"/> descendente
    /// como desempate.
    /// </summary>
    /// <remarks>
    /// La consulta utiliza <c>AsNoTracking</c> porque es de solo lectura.
    /// </remarks>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Colección de solo lectura con todos los ingresos ordenados.
    /// </returns>
    public async Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Incomes
            .AsNoTracking()
            .Include(i => i.PaymentMethod)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Obtiene una página de <see cref="Income"/> que cumplen con el
    /// filtro especificado.
    /// </summary>
    /// <remarks>
    /// No valida el filtro: confía en que el caller (F-008
    /// <c>GetIncomesQuery</c>) garantiza <see cref="IncomeFilter.Page"/>
    /// ≥ 1, <see cref="IncomeFilter.PageSize"/> ≥ 1 y coherencia de fechas.
    /// <para>
    /// Aplica <see cref="IncomeFilter.TagIds"/> con semántica OR/any-match
    /// (F-007): devuelve ingresos que tengan al menos uno de los tags.
    /// </para>
    /// </remarks>
    /// <param name="filter">Filtro de paginación y criterios de búsqueda.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="PagedResult{Income}"/> con los items de la página
    /// solicitada y los metadatos de paginación.
    /// </returns>
    public async Task<PagedResult<Income>> GetByFilterAsync(IncomeFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<Income> query = _db.Incomes
            .AsNoTracking()
            .Include(i => i.PaymentMethod);

        if (filter.CategoryId is not null)
        {
            query = query.Where(e => e.CategoryId == filter.CategoryId);
        }

        if (filter.DateFrom is not null)
        {
            query = query.Where(e => e.Date >= filter.DateFrom);
        }

        if (filter.DateTo is not null)
        {
            query = query.Where(e => e.Date <= filter.DateTo);
        }

        if (filter.TagIds is { Count: > 0 })
        {
            query = query.Where(i => i.Tags.Any(t => filter.TagIds!.Contains(t.Id)));
        }

        if (filter.PaymentMethodIds is { Count: > 0 })
        {
            // Solo incluimos ingresos que tengan PaymentMethodId no nulo
            // Y que esté en la lista seleccionada. Los ingresos sin método
            // quedan fuera cuando el filtro está activo (semántica OR/IN).
            query = query.Where(i =>
                i.PaymentMethodId != null
                && filter.PaymentMethodIds!.Contains(i.PaymentMethodId.Value));
        }

        query = query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt);

        int total = await query.CountAsync(ct);
        int skip = (filter.Page - 1) * filter.PageSize;
        List<Income> items = await query
            .Skip(skip)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Income>(items, total, filter.Page, filter.PageSize);
    }
}
