namespace ControlDeFinanzasIA.UI.Input;

/// <summary>
/// Implemented by a view-model that can be in the middle of an operation that navigation shortcuts must not
/// interrupt (an import commit, a restore). It only reads existing state: it never owns any (F-021, DD-6).
/// </summary>
public interface IBusyAware
{
    /// <summary>Gets a value indicating whether an operation that shortcuts must not interrupt is running.</summary>
    bool IsBusy { get; }
}
