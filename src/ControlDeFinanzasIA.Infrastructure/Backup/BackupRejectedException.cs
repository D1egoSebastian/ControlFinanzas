using System.Diagnostics.CodeAnalysis;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Infrastructure.Backup;

/// <summary>
/// Internal control-flow signal of the validation pipeline: a backup was rejected with a stable code.
/// Always caught at the <see cref="SqliteBackupService"/> boundary and turned into a <see cref="Result"/>.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Internal signal carrying a prepared Result.")]
internal sealed class BackupRejectedException(Result failure) : Exception(failure.Error?.Message)
{
    /// <summary>The failure to hand back to the caller.</summary>
    public Result Failure { get; } = failure;
}
