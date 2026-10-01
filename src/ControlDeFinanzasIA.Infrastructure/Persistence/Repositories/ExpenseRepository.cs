using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Aggregates;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
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

        return await _db.Expenses
            .Include(e => e.PaymentMethod)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public Task<Expense?> GetByRecurringRuleAndPeriodAsync(Guid recurringRuleId, DateOnly period, CancellationToken ct = default) =>
        _db.Expenses.FirstOrDefaultAsync(e => e.RecurringRuleId == recurringRuleId && e.RecurringPeriod == period, ct);

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
            .Include(e => e.PaymentMethod)
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

        IQueryable<Expense> query = _db.Expenses
            .AsNoTracking()
            .Include(e => e.PaymentMethod);

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

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            string pattern = TextSearch.ContainsPattern(filter.Text);
            query = query.Where(e => EF.Functions.Like(e.Description, pattern, TextSearch.EscapeCharacter));
        }

        if (filter.TagIds is { Count: > 0 })
        {
            query = query.Where(e => e.Tags.Any(t => filter.TagIds!.Contains(t.Id)));
        }

        if (filter.PaymentMethodIds is { Count: > 0 })
        {
            // Solo incluimos gastos que tengan PaymentMethodId no nulo
            // Y que esté en la lista seleccionada. Los gastos sin método
            // quedan fuera cuando el filtro está activo (semántica OR/IN).
            query = query.Where(e =>
                e.PaymentMethodId != null
                && filter.PaymentMethodIds!.Contains(e.PaymentMethodId.Value));
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

    /// <summary>
    /// Obtiene los totales de gasto por moneda dentro de
    /// <paramref name="period"/> mediante <c>GROUP BY</c>/<c>SUM</c> en SQL (R-3).
    /// </summary>
    /// <remarks>
    /// El proveedor SQLite de EF Core 8 lanza <c>NotSupportedException</c>
    /// ("SQLite cannot apply aggregate operator 'Sum' on expressions of type
    /// 'decimal'") si se intenta <c>Sum</c> directamente sobre la columna
    /// <c>decimal</c>: SQLite no tiene un tipo decimal nativo y el traductor
    /// de agregados de este proveedor no soporta <c>SUM</c> sobre ese tipo
    /// CLR, sin importar la clave de agrupación. El <c>SUM</c> sigue
    /// ejecutándose en SQL (satisface R-3); solo se castea a <c>double</c>
    /// para que el proveedor lo traduzca, y se vuelve a <c>decimal</c> al
    /// proyectar. Con la precisión de columna (18,4) y magnitudes de un
    /// presupuesto personal, esto no pierde precisión significativa.
    /// </remarks>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    public async Task<IReadOnlyList<CurrencyTotal>> GetPeriodTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        List<CurrencyTotal> totals = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .GroupBy(e => e.Amount.Currency)
            .Select(g => new CurrencyTotal(g.Key, (decimal)g.Sum(x => (double)x.Amount.Amount)))
            .ToListAsync(ct);

        return totals.Select(t => t with { Amount = Math.Round(t.Amount, 4) }).ToList();
    }

    /// <summary>
    /// Obtiene los totales de gasto por categoría x moneda dentro de
    /// <paramref name="period"/> mediante <c>GROUP BY</c>/<c>SUM</c> en SQL,
    /// con join a <c>Categories</c> para la etiqueta. Devuelve todas las
    /// categorías con gasto en el periodo; el ranking top-N y "Otras" se
    /// calculan en la capa Application (D-2).
    /// </summary>
    /// <remarks>
    /// Mismo workaround de cast a <c>double</c> que <see cref="GetPeriodTotalsAsync"/>
    /// — ver esa nota para el porqué.
    /// </remarks>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    public async Task<IReadOnlyList<CategoryCurrencyTotal>> GetCategoryTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        List<CategoryCurrencyTotal> totals = await (from e in _db.Expenses.AsNoTracking()
                       where e.Date >= period.Start && e.Date <= period.End
                       join c in _db.Categories.AsNoTracking() on e.CategoryId equals c.Id
                       group e by new { c.Id, c.Name, e.Amount.Currency } into g
                       select new CategoryCurrencyTotal(
                           g.Key.Id, g.Key.Name, g.Key.Currency, (decimal)g.Sum(x => (double)x.Amount.Amount)))
                      .ToListAsync(ct);

        return totals.Select(t => t with { Amount = Math.Round(t.Amount, 4) }).ToList();
    }

    /// <summary>
    /// Obtiene los totales de gasto por mes x moneda dentro de
    /// <paramref name="period"/> mediante <c>GROUP BY</c>/<c>SUM</c> en SQL.
    /// Usado por el gráfico de evolución temporal (R-5).
    /// </summary>
    /// <remarks>
    /// Mismo workaround de cast a <c>double</c> que <see cref="GetPeriodTotalsAsync"/>
    /// — ver esa nota para el porqué. <c>Date.Year</c>/<c>Date.Month</c> sí
    /// traducen correctamente a <c>strftime</c> en el proveedor SQLite de EF
    /// Core 8 (confirmado por <c>DashboardAggregateQueryTests</c>); el único
    /// problema real era el <c>Sum</c> sobre <c>decimal</c>.
    /// </remarks>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    public async Task<IReadOnlyList<MonthCurrencyTotal>> GetMonthlyTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        List<MonthCurrencyTotal> totals = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .GroupBy(e => new { e.Date.Year, e.Date.Month, e.Amount.Currency })
            .Select(g => new MonthCurrencyTotal(g.Key.Year, g.Key.Month, g.Key.Currency, (decimal)g.Sum(x => (double)x.Amount.Amount)))
            .ToListAsync(ct);

        return totals.Select(t => t with { Amount = Math.Round(t.Amount, 4) }).ToList();
    }

    /// <summary>
    /// Obtiene los totales de gasto por día x moneda dentro de
    /// <paramref name="period"/>, agregados en SQL (<c>GROUP BY</c>/<c>SUM</c>).
    /// Usado por la vista de calendario (R-5).
    /// </summary>
    /// <remarks>
    /// Mismo patrón de cast a <c>double</c> + <c>Math.Round(_, 4)</c>
    /// post-materialización que <see cref="GetMonthlyTotalsAsync"/> — SQLite
    /// no soporta <c>Sum(decimal)</c> (ver <see cref="GetPeriodTotalsAsync"/>).
    /// <c>e.Date</c> agrupa sobre la columna <see cref="DateOnly"/> cruda,
    /// sin traducción <c>strftime</c>.
    /// </remarks>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    public async Task<IReadOnlyList<DayCurrencyTotal>> GetDailyTotalsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        List<DayCurrencyTotal> totals = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .GroupBy(e => new { e.Date, e.Amount.Currency })
            .Select(g => new DayCurrencyTotal(g.Key.Date, g.Key.Currency, (decimal)g.Sum(x => (double)x.Amount.Amount)))
            .ToListAsync(ct);

        return totals.Select(t => t with { Amount = Math.Round(t.Amount, 4) }).ToList();
    }

    /// <summary>
    /// Obtiene una fila por cada <see cref="Expense"/> dentro de
    /// <paramref name="period"/>, sin agregar (F-014, D-7).
    /// </summary>
    /// <remarks>
    /// Proyección plana: no hay <c>Sum</c> en SQL, por lo que no necesita
    /// el workaround de cast a <c>double</c> que documenta
    /// <see cref="GetPeriodTotalsAsync"/>. Ordenada por <c>Date</c> luego
    /// <c>Id</c> para dar un orden determinístico a
    /// <c>DetectAnomaliesUseCase</c>.
    /// </remarks>
    /// <param name="period">Rango de fechas, inclusivo en ambos extremos.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    public async Task<IReadOnlyList<ExpenseAmountRow>> GetAmountRowsAsync(DateRange period, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        return await _db.Expenses
            .AsNoTracking()
            .Where(e => e.Date >= period.Start && e.Date <= period.End)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .Select(e => new ExpenseAmountRow(e.Id, e.CategoryId, e.Date, e.Amount.Amount, e.Amount.Currency, e.Description))
            .ToListAsync(ct);
    }
}
