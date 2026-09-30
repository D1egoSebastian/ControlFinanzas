namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>Options for <see cref="ITabularReader"/> (F-020, DD-1).</summary>
/// <param name="Delimiter">Field delimiter; <c>null</c> sniffs it (CSV only, ignored for Excel).</param>
public sealed record TabularReadOptions(char? Delimiter);
