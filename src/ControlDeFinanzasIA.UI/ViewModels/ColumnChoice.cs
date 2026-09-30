namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>One entry of a column picker: a header of the file, or "(none)" when <paramref name="Index"/> is <c>null</c>.</summary>
/// <param name="Index">Zero-based header index; <c>null</c> means no column.</param>
/// <param name="Label">Text shown to the user.</param>
public sealed record ColumnChoice(int? Index, string Label);
