using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Mapping extensions from <see cref="Category"/> domain entities to DTOs.
/// </summary>
public static class CategoryExtensions
{
    /// <summary>
    /// Maps a <see cref="Category"/> to a <see cref="CategoryDto"/>.
    /// </summary>
    /// <param name="category">The category to map.</param>
    /// <returns>A trimmed DTO with the category identity and name.</returns>
    public static CategoryDto ToDto(this Category category)
        => new(category.Id, category.Name);
}
