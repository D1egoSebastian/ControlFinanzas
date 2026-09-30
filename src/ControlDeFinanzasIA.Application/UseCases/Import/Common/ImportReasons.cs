namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>Severity helpers for <see cref="ImportReason"/> (F-020, DD-2).</summary>
public static class ImportReasons
{
    /// <summary>Returns whether the reason rejects the row (as opposed to warning about it).</summary>
    /// <param name="reason">The reason.</param>
    public static bool IsRejection(ImportReason reason) => reason < ImportReason.FutureDate;
}
