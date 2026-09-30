namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>What a committed import did (F-020, DD-1).</summary>
/// <param name="ExpensesInserted">Expenses written.</param>
/// <param name="IncomesInserted">Incomes written.</param>
/// <param name="CategoriesCreated">Categories created.</param>
/// <param name="DuplicatesSkipped">Duplicate rows skipped.</param>
/// <param name="Rejected">Rejected rows.</param>
public sealed record ImportCommitResult(
    int ExpensesInserted,
    int IncomesInserted,
    int CategoriesCreated,
    int DuplicatesSkipped,
    int Rejected);
