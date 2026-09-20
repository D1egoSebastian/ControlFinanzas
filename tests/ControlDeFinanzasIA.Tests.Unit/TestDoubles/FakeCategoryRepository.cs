using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ICategoryRepository"/> used in unit tests.
/// <see cref="GetAllAsync"/> and <see cref="GetByIdAsync"/> resolve against
/// <see cref="Categories"/>; the remaining members are stubs because no
/// current consumer under test invokes them.
/// </summary>
public sealed class FakeCategoryRepository : ICategoryRepository
{
    /// <summary>
    /// Gets or sets the categories returned by <see cref="GetAllAsync"/>.
    /// </summary>
    public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();

    /// <summary>
    /// Replaces the configured categories.
    /// </summary>
    /// <param name="categories">Categories to return from <see cref="GetAllAsync"/>.</param>
    public void SetCategories(IEnumerable<Category> categories)
        => Categories = categories.ToList();

    /// <inheritdoc />
    public Task<Result> AddAsync(Category category, CancellationToken ct = default)
        => Task.FromResult(Result.Success(null!));

    /// <inheritdoc />
    public Task<Result> UpdateAsync(Category category, CancellationToken ct = default)
        => Task.FromResult(Result.Success(null!));

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Result.Success(null!));

    /// <inheritdoc />
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(Categories);
}
