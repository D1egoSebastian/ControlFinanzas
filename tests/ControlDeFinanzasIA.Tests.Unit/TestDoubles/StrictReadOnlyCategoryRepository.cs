using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ICategoryRepository"/> whose write
/// members throw (F-013, slice 5b, D-6, S-11). <see cref="ICategoryRepository"/>
/// cannot express "read-only" in its type, so this double enforces it at
/// test time: any future edit to <c>CategorizeExpenseUseCase</c> that
/// creates, updates or deletes a category fails a test with a clear
/// message instead of silently polluting the user's catalog.
/// </summary>
public sealed class StrictReadOnlyCategoryRepository : ICategoryRepository
{
    /// <summary>Gets or sets the categories returned by <see cref="GetAllAsync"/>.</summary>
    public IReadOnlyList<Category> Categories { get; set; } = [];

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Always. The AI categorization path must never create a category.</exception>
    public Task<Result> AddAsync(Category category, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "AddAsync must never be called by the AI categorization path (F-013, D-6, S-11).");

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Always. The AI categorization path must never update a category.</exception>
    public Task<Result> UpdateAsync(Category category, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "UpdateAsync must never be called by the AI categorization path (F-013, D-6, S-11).");

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Always. The AI categorization path must never delete a category.</exception>
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "RemoveAsync must never be called by the AI categorization path (F-013, D-6, S-11).");

    /// <inheritdoc />
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(Categories);
}
