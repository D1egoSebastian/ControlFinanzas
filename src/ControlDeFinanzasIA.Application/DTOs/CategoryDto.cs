using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Data transfer object that represents a <see cref="Category"/> in the
/// application layer. Trimmed to the minimal fields needed by the UI category picker.
/// </summary>
public sealed record CategoryDto(Guid Id, string Name);
