using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Exports.Common;

/// <summary>
/// Reads every page of a paged source for an export (F-018, R-7, D-5). Page 1 fixes the
/// expected total; the loop stops when enough rows were collected, on an empty page (rows
/// deleted mid-export) or at the hard cap of <c>ceil(total / PageSize)</c> pages.
/// </summary>
internal static class ReportPaging
{
    /// <summary>Rows requested per page.</summary>
    internal const int PageSize = 200;

    internal static async Task<IReadOnlyList<T>> ReadAllAsync<T>(
        Func<int, CancellationToken, Task<PagedResult<T>>> fetchPage,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fetchPage);

        PagedResult<T> first = await fetchPage(1, ct).ConfigureAwait(false);
        int expectedTotal = first.Total;
        var collected = new List<T>(first.Items);
        int maxPages = (expectedTotal + PageSize - 1) / PageSize;
        int page = 1;

        while (collected.Count < expectedTotal && page < maxPages)
        {
            ct.ThrowIfCancellationRequested();
            page++;
            PagedResult<T> next = await fetchPage(page, ct).ConfigureAwait(false);
            if (next.Items.Count == 0)
            {
                break;
            }

            collected.AddRange(next.Items);
        }

        return collected;
    }
}
