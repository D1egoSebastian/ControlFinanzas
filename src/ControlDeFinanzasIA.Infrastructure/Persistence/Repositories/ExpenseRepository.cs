using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación concreta de <see cref="IExpenseRepository"/> basada en
/// Entity Framework Core y el <see cref="AppDbContext"/> del proyecto.
/// </summary>
public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="ExpenseRepository"/>.
    /// </summary>
    /// <param name="db">Contexto de EF Core utilizado para persistir los gastos.</param>
    public ExpenseRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Añade un nuevo <see cref="Expense"/> a la base de datos.
    /// </summary>
    /// <remarks>
    /// Valida que la categoría referenciada por <see cref="Expense.CategoryId"/>
    /// exista antes de persistir. Si no existe, devuelve
    /// <c>Result.Failure("CategoryNotFound", ...)</c>.
    /// </remarks>
    /// <param name="expense">Gasto a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se persistió; de lo contrario,
    /// un error con código <c>CategoryNotFound</c>.
    /// </returns>
    public async Task<Result> AddAsync(Expense expense, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(expense);

        Category? category = await _db.Categories.FindAsync(new object?[] { expense.CategoryId }, ct);
        if (category is null)
        {
            return Result.Failure(
                "CategoryNotFound",
                $"La categoría {expense.CategoryId} no existe.");
        }

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Actualiza un <see cref="Expense"/> existente.
    /// </summary>
    /// <remarks>
    /// Si el gasto no existe, devuelve <c>Result.Failure("NotFound", ...)</c>.
    /// No re-valida la FK a <c>Categories</c>; el use case caller es
    /// responsable de la coherencia cross-aggregate.
    /// </remarks>
    /// <param name="expense">Gasto con los valores actualizados.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se actualizó; de lo contrario,
    /// un error con código <c>NotFound</c>.
    /// </returns>
    public async Task<Result> UpdateAsync(Expense expense, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(expense);

        Expense? existing = await _db.Expenses.FindAsync(new object?[] { expense.Id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Gasto {expense.Id} no encontrado.");
        }

        _db.Entry(existing).CurrentValues.SetValues(expense);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Elimina un <see cref="Expense"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Realiza un hard delete: la fila se elimina físicamente de la tabla
    /// <c>Expenses</c>. Si el gasto no existe, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>.
    /// </remarks>
    /// <param name="id">Identificador del gasto a eliminar.</param>
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
                $"Gasto {id} no encontrado.");
        }

        Expense? existing = await _db.Expenses.FindAsync(new object?[] { id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Gasto {id} no encontrado.");
        }

        _db.Expenses.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Obtiene un <see cref="Expense"/> por su identificador.
    /// </summary>
    /// <param name="id">Identificador del gasto.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// El gasto encontrado, o <see langword="null"/> si no existe.
    /// </returns>
    public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _db.Expenses.FindAsync(new object?[] { id }, ct);
    }

    /// <summary>
    /// Obtiene todos los <see cref="Expense"/> ordenados por fecha
    /// descendente, con <see cref="Expense.CreatedAt"/> descendente
    /// como desempate.
    /// </summary>
    /// <remarks>
    /// La consulta utiliza <c>AsNoTracking</c> porque es de solo lectura.
    /// </remarks>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Colección de solo lectura con todos los gastos ordenados.
    /// </returns>
    public async Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Expenses
            .AsNoTracking()
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Obtiene una página de <see cref="Expense"/> que cumplen con el
    /// filtro especificado.
    /// </summary>
    /// <remarks>
    /// No valida el filtro: confía en que el caller (F-005
    /// <c>GetTransactionsQuery</c>) garantiza <see cref="ExpenseFilter.Page"/>
    /// ≥ 1, <see cref="ExpenseFilter.PageSize"/> ≥ 1 y coherencia de fechas.
    /// <para>
    /// Aplica <see cref="ExpenseFilter.TagIds"/> con semántica OR/any-match
    /// (F-007): devuelve gastos que tengan al menos uno de los tags.
    /// </para>
    /// </remarks>
    /// <param name="filter">Filtro de paginación y criterios de búsqueda.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="PagedResult{Expense}"/> con los items de la página
    /// solicitada y los metadatos de paginación.
    /// </returns>
    public async Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<Expense> query = _db.Expenses.AsNoTracking();

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
            query = query.Where(e => e.Tags.Any(t => filter.TagIds!.Contains(t.Id)));
        }

        query = query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt);

        int total = await query.CountAsync(ct);
        int skip = (filter.Page - 1) * filter.PageSize;
        List<Expense> items = await query
            .Skip(skip)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Expense>(items, total, filter.Page, filter.PageSize);
    }
}
