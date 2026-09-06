using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ITagRepository"/> used in unit tests.
/// Captures added tags and allows configuring the results returned by
/// each method.
/// </summary>
public sealed class FakeTagRepository : ITagRepository
{
    private readonly List<Tag> _addedTags = new();

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
    /// Gets or sets the tag returned by <see cref="GetByIdAsync"/>.
    /// When <c>null</c> the fake searches the captured list.
    /// </summary>
    public Tag? GetByIdResultOverride { get; set; }

    /// <summary>
    /// Gets the tags passed to <see cref="AddAsync"/>.
    /// </summary>
    public IReadOnlyList<Tag> AddedTags => _addedTags;

    /// <inheritdoc />
    public Task<Result> AddAsync(Tag tag, CancellationToken ct = default)
    {
        _addedTags.Add(tag);
        return Task.FromResult(AddResultOverride ?? Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(Tag tag, CancellationToken ct = default)
    {
        if (UpdateResultOverride is not null)
        {
            return Task.FromResult(UpdateResultOverride);
        }

        _addedTags.RemoveAll(t => t.Id == tag.Id);
        _addedTags.Add(tag);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (RemoveResultOverride is not null)
        {
            return Task.FromResult(RemoveResultOverride);
        }

        _addedTags.RemoveAll(t => t.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (GetByIdResultOverride is not null)
        {
            return Task.FromResult<Tag?>(GetByIdResultOverride);
        }

        return Task.FromResult<Tag?>(_addedTags.FirstOrDefault(t => t.Id == id));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Tag>>(_addedTags.ToList());
}
