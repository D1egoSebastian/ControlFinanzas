using ControlDeFinanzasIA.Application.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>Proves the multi-filter picker overload and its recording double (F-020, D-20).</summary>
public sealed class FakeFileOpenDialogTests
{
    private static readonly FileTypeFilter _csv = new("CSV", "csv", "text/csv");
    private static readonly FileTypeFilter _xlsx = new("Excel", "xlsx", "application/vnd.ms-excel");

    [Fact]
    public async Task ListOverload_RecordsEveryFilterInOrder_AndExposesTheFirstAsLastFilter()
    {
        var dialog = new FakeFileOpenDialog();

        OpenTarget? result = await dialog.PickAsync([_csv, _xlsx], "Pick");

        Assert.Null(result);
        Assert.Equal(1, dialog.Calls);
        Assert.Equal([_csv, _xlsx], dialog.LastFilters);
        Assert.Equal(_csv, dialog.LastFilter);
        Assert.Equal("Pick", dialog.LastTitle);
    }

    [Fact]
    public async Task SingleFilterOverload_DelegatesToTheListOverload()
    {
        var dialog = new FakeFileOpenDialog();

        await dialog.PickAsync(_xlsx, "One");

        Assert.Equal(1, dialog.Calls);
        Assert.Equal([_xlsx], dialog.LastFilters);
        Assert.Equal(_xlsx, dialog.LastFilter);
    }

    [Fact]
    public async Task ListOverload_ReturnsTheConfiguredTarget()
    {
        using var target = new OpenTarget("a.csv", _ => Task.FromResult<Stream>(new MemoryStream()));
        var dialog = new FakeFileOpenDialog { Result = target };

        OpenTarget? result = await dialog.PickAsync([_csv]);

        Assert.Same(target, result);
    }

    [Fact]
    public async Task InterfaceDefault_FallsBackToTheFirstFilter_ForImplementersThatDoNotOverrideIt()
    {
        var single = new SingleFilterOnlyDialog();
        IFileOpenDialog dialog = single;

        await dialog.PickAsync([_xlsx, _csv], "T");

        Assert.Equal(_xlsx, single.Received);
    }

    [Fact]
    public async Task InterfaceDefault_RejectsAnEmptyFilterList()
    {
        IFileOpenDialog dialog = new SingleFilterOnlyDialog();

        await Assert.ThrowsAsync<ArgumentException>(() => dialog.PickAsync(Array.Empty<FileTypeFilter>()));
    }

    private sealed class SingleFilterOnlyDialog : IFileOpenDialog
    {
        public FileTypeFilter? Received { get; private set; }

        public Task<OpenTarget?> PickAsync(FileTypeFilter filter, string? title = null, CancellationToken ct = default)
        {
            Received = filter;
            return Task.FromResult<OpenTarget?>(null);
        }
    }
}
