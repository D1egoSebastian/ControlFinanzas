using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Transactions;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;

/// <summary>
/// Query de lectura que devuelve una página de gastos mapeados a <see cref="ExpenseDto"/>.
/// </summary>
/// <remarks>
/// El método retorna un <see cref="Result"/> non-genérico. En caso de éxito,
/// el caller debe castear <see cref="Result.Value"/> a
/// <see cref="PagedResult{ExpenseDto}"/> tras verificar <see cref="Result.IsSuccess"/>.
/// </remarks>
public sealed class GetTransactionsQuery
{
    private readonly IExpenseRepository _repository;
    private readonly ILogger<GetTransactionsQuery> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del query.
    /// </summary>
    /// <param name="repository">Repositorio de gastos.</param>
    /// <param name="logger">Logger para diagnosticar errores del repositorio.</param>
    public GetTransactionsQuery(IExpenseRepository repository, ILogger<GetTransactionsQuery> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el query de paginación de gastos.
    /// </summary>
    /// <param name="request">Parámetros de paginación.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con <see cref="PagedResult{ExpenseDto}"/> en <see cref="Result.Value"/>
    /// si la operación tiene éxito; de lo contrario, un <see cref="Error"/> con código
    /// <c>"InvalidFilter"</c> o <c>"RepositoryError"</c>.
    /// </returns>
    public async Task<Result> ExecuteAsync(GetTransactionsQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Page < 1)
        {
            return Result.Failure("InvalidFilter", "Page debe ser mayor o igual a 1.");
        }

        if (request.PageSize <= 0)
        {
            return Result.Failure("InvalidFilter", "PageSize debe ser mayor a 0.");
        }

        if (request.PageSize > 200)
        {
            return Result.Failure("InvalidFilter", "PageSize debe ser menor o igual a 200.");
        }

        if (request.DateFrom.HasValue && request.DateTo.HasValue && request.DateFrom > request.DateTo)
        {
            return Result.Failure("InvalidFilter", "DateFrom no puede ser mayor que DateTo.");
        }

        if (request.TagIds is not null && request.TagIds.Any(id => id == Guid.Empty))
        {
            return Result.Failure("InvalidFilter", "TagIds no puede contener identificadores vacíos.");
        }

        ExpenseFilter filter = new()
        {
            Page = request.Page,
            PageSize = request.PageSize,
            CategoryId = request.CategoryId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            TagIds = request.TagIds,
        };

        try
        {
            PagedResult<Expense> page = await _repository.GetByFilterAsync(filter, ct);
            var items = page.Items.Select(e => e.ToDto()).ToList();
            return Result.Success(new PagedResult<ExpenseDto>(items, page.Total, filter.Page, filter.PageSize));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener gastos (Page={Page}, PageSize={PageSize})", filter.Page, filter.PageSize);
            return Result.Failure("RepositoryError", "Ocurrió un error al obtener los gastos.");
        }
    }
}
