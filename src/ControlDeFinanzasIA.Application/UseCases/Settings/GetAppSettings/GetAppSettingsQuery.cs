using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Settings.GetAppSettings;

/// <summary>
/// Query que lee el modelo de preferencias completo del usuario (F-012,
/// D-9). Delega en <see cref="ISettingsStore.GetAppSettingsAsync"/>, que
/// nunca lanza ni retorna <see langword="null"/> (D-3), por lo que este
/// resultado siempre es exitoso.
/// </summary>
public sealed class GetAppSettingsQuery
{
    private readonly ISettingsStore _store;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetAppSettingsQuery(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>
    /// Ejecuta la query.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> exitoso con las
    /// <see cref="Settings.AppSettings"/> actuales en <see cref="Result.Value"/>.
    /// No puede fallar (D-3).
    /// </returns>
    public async Task<Result> ExecuteAsync(CancellationToken ct = default)
        => Result.Success(await _store.GetAppSettingsAsync(ct).ConfigureAwait(false));
}
