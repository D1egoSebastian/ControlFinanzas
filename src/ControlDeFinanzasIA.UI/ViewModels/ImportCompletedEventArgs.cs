using ControlDeFinanzasIA.Application.UseCases.Import.Common;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>Raised by <see cref="ImportViewModel"/> after a successful import so the host can reload (F-020, R-22).</summary>
public sealed class ImportCompletedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ImportCompletedEventArgs"/> class.</summary>
    /// <param name="result">What the import wrote.</param>
    public ImportCompletedEventArgs(ImportCommitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
    }

    /// <summary>Gets what the import wrote.</summary>
    public ImportCommitResult Result { get; }

    /// <summary>Gets the number of transactions (expenses and incomes) that were inserted.</summary>
    public int Inserted => Result.ExpensesInserted + Result.IncomesInserted;
}
