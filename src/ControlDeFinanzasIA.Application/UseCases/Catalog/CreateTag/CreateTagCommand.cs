namespace ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;

/// <summary>
/// Command that carries the data required to create a new <see cref="Domain.Catalog.Tag"/>.
/// </summary>
/// <param name="Name">Display name of the tag.</param>
/// <param name="Color">Optional color code (e.g., <c>#FF8800</c>).</param>
public sealed record CreateTagCommand(string Name, string? Color);
