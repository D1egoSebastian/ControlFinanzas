using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Exports.Common;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Exports;

/// <summary>
/// Tests for <see cref="ReportPaging"/> (F-018, R-7, S-9, D-5): reading every page of a
/// paged source with a bounded, deletion-tolerant loop.
/// </summary>
public sealed class ReportPagingTests
{
    private static Func<int, CancellationToken, Task<PagedResult<int>>> Source(int total, List<int> requestedPages)
    {
        return (page, _) =>
        {
            requestedPages.Add(page);
            int skip = (page - 1) * ReportPaging.PageSize;
            var items = Enumerable.Range(0, total).Skip(skip).Take(ReportPaging.PageSize).ToList();
            return Task.FromResult(new PagedResult<int>(items, total, page, ReportPaging.PageSize));
        };
    }

    [Fact]
    public async Task ReadAllAsync_450Rows_ReadsThreePagesOf200_200_50()
    {
        var pages = new List<int>();

        IReadOnlyList<int> rows = await ReportPaging.ReadAllAsync(Source(450, pages), CancellationToken.None);

        Assert.Equal(450, rows.Count);
        Assert.Equal(new[] { 1, 2, 3 }, pages);
        Assert.Equal(Enumerable.Range(0, 450), rows);
    }

    [Fact]
    public async Task ReadAllAsync_ZeroRows_ReturnsEmptyAfterSingleCall()
    {
        var pages = new List<int>();

        IReadOnlyList<int> rows = await ReportPaging.ReadAllAsync(Source(0, pages), CancellationToken.None);

        Assert.Empty(rows);
        Assert.Single(pages);
    }

    [Fact]
    public async Task ReadAllAsync_ExactPageMultiple_DoesNotReadExtraPage()
    {
        var pages = new List<int>();

        IReadOnlyList<int> rows = await ReportPaging.ReadAllAsync(Source(400, pages), CancellationToken.None);

        Assert.Equal(400, rows.Count);
        Assert.Equal(new[] { 1, 2 }, pages);
    }

    [Fact]
    public async Task ReadAllAsync_EmptyPageMidExport_BreaksTheLoop()
    {
        int calls = 0;
        Task<PagedResult<int>> Fetch(int page, CancellationToken ct)
        {
            calls++;
            IReadOnlyList<int> items = page == 1 ? Enumerable.Range(0, 200).ToList() : Array.Empty<int>();
            return Task.FromResult(new PagedResult<int>(items, 450, page, ReportPaging.PageSize));
        }

        IReadOnlyList<int> rows = await ReportPaging.ReadAllAsync(Fetch, CancellationToken.None);

        Assert.Equal(200, rows.Count);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ReadAllAsync_SourceKeepsReturningRows_IsCappedAtCeilTotalOver200Pages()
    {
        int calls = 0;
        Task<PagedResult<int>> Fetch(int page, CancellationToken ct)
        {
            calls++;
            // Total reported as 30, but the source claims a "full" page forever after page 1.
            IReadOnlyList<int> items = Enumerable.Range(0, 10).ToList();
            return Task.FromResult(new PagedResult<int>(items, 450, page, ReportPaging.PageSize));
        }

        IReadOnlyList<int> rows = await ReportPaging.ReadAllAsync(Fetch, CancellationToken.None);

        Assert.Equal(3, calls); // ceil(450 / 200)
        Assert.Equal(30, rows.Count);
    }

    [Fact]
    public async Task ReadAllAsync_PassesCancellationTokenToFetch()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;
        Task<PagedResult<int>> Fetch(int page, CancellationToken ct)
        {
            seen = ct;
            return Task.FromResult(new PagedResult<int>(Array.Empty<int>(), 0, page, ReportPaging.PageSize));
        }

        await ReportPaging.ReadAllAsync(Fetch, cts.Token);

        Assert.Equal(cts.Token, seen);
    }
}
