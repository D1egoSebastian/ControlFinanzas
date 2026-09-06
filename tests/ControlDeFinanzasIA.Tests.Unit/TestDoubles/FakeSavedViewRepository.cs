using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ISavedViewRepository"/> used in unit tests.
/// Captures added saved views and allows configuring the results returned by
/// each method.
/// </summary>
public sealed class FakeSavedViewRepository : ISavedViewRepository
{
    private readonly List<SavedView> _addedSavedViews = new();

    /// <summary>
    /// Gets or sets the result returned by <see cref="AddAsync"/>.
    /// When <c>null</c> the fake performs an in-memory add.
    /// </summary>
    public Result? AddResultOverride { get; set; }

    /// <summary>
    /// Gets or sets the result returned by <see cref="UpdateAsync"/>.
    /// When <c>null</c> the fake performs an in-memory update.
    /// </summary>
    public Result? UpdateResultOverride { get; set; }

    /// <summary>
    /// Gets or sets the result returned by <see cref="RemoveAsync"/>.
    /// When <c>null</c> the fake performs an in-memory removal.
    /// </summary>
    public Result? RemoveResultOverride { get; set; }

    /// <summary>
    /// Gets or sets the saved view returned by <see cref="GetByIdAsync"/>.
    /// When <c>null</c> the fake searches the captured list.
    /// </summary>
    public SavedView? GetByIdResultOverride { get; set; }

    /// <summary>
    /// Gets the saved views passed to <see cref="AddAsync"/>.
    /// </summary>
    public IReadOnlyList<SavedView> AddedSavedViews => _addedSavedViews;

    /// <inheritdoc />
    public Task<Result> AddAsync(SavedView savedView, CancellationToken ct = default)
    {
        _addedSavedViews.Add(savedView);
        return Task.FromResult(AddResultOverride ?? Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(SavedView savedView, CancellationToken ct = default)
    {
        if (UpdateResultOverride is not null)
        {
            return Task.FromResult(UpdateResultOverride);
        }

        _addedSavedViews.RemoveAll(s => s.Id == savedView.Id);
        _addedSavedViews.Add(savedView);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (RemoveResultOverride is not null)
        {
            return Task.FromResult(RemoveResultOverride);
        }

        _addedSavedViews.RemoveAll(s => s.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<SavedView?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (GetByIdResultOverride is not null)
        {
            return Task.FromResult<SavedView?>(GetByIdResultOverride);
        }

        return Task.FromResult<SavedView?>(_addedSavedViews.FirstOrDefault(s => s.Id == id));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SavedView>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SavedView>>(_addedSavedViews.ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<SavedView>> GetByEntityTypeAsync(
        TransactionType entityType,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SavedView>>(
            _addedSavedViews.Where(s => s.EntityType == entityType).ToList());
}
