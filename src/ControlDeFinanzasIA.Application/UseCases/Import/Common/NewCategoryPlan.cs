namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>A category the import will create (F-020, DD-7, DD-11).</summary>
/// <param name="FoldKey">The case- and diacritics-insensitive key that identifies the category inside the batch.</param>
/// <param name="Name">The name to store: the first spelling seen, trimmed, whitespace collapsed.</param>
public sealed record NewCategoryPlan(string FoldKey, string Name);
