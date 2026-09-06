namespace ControlDeFinanzasIA.Application.UseCases.Incomes.GetIncomes;

/// <summary>
/// Immutable request for <see cref="GetIncomesQuery"/>.
/// Exposes pagination and day-one filters that are mapped 1:1 to
/// <see cref="Abstractions.IncomeFilter"/>.
/// </summary>
/// <param name="Page">Requested page number (1-based). Default: 1.</param>
/// <param name="PageSize">Items per page. Default: 50.</param>
/// <param name="CategoryId">Optional category filter.</param>
/// <param name="DateFrom">Optional inclusive lower bound of the date range.</param>
/// <param name="DateTo">Optional inclusive upper bound of the date range.</param>
/// <param name="TagIds">Optional tag identifiers for OR-matching.</param>
public sealed record GetIncomesQueryRequest(
    int Page = 1,
    int PageSize = 50,
    Guid? CategoryId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    IReadOnlyList<Guid>? TagIds = null);
