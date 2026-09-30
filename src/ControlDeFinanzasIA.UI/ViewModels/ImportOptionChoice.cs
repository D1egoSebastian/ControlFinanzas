namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>One localized entry of an import option picker.</summary>
/// <typeparam name="T">Type of the option value.</typeparam>
/// <param name="Value">The option value.</param>
/// <param name="Label">Text shown to the user.</param>
public sealed record ImportOptionChoice<T>(T Value, string Label);
