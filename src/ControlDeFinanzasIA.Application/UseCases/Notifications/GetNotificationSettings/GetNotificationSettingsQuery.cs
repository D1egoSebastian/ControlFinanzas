using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Notifications;

namespace ControlDeFinanzasIA.Application.UseCases.Notifications.GetNotificationSettings;

/// <summary>
/// Query que lee las preferencias de notificaciones vigentes (F-017, R-12,
/// R-13). Delega en <see cref="NotificationSettingsReader.ReadAsync"/>, que
/// nunca lanza y siempre resuelve a un default documentado, por lo que
/// este resultado siempre es exitoso.
/// </summary>
public sealed class GetNotificationSettingsQuery
{
    private readonly ISettingsStore _store;

    /// <summary>
    /// Inicializa una nueva instancia de la query.
    /// </summary>
    public GetNotificationSettingsQuery(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>
    /// Ejecuta la query.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> exitoso con las <see cref="NotificationSettings"/>
    /// vigentes en <see cref="Result.Value"/>. No puede fallar.
    /// </returns>
    public async Task<Result> ExecuteAsync(CancellationToken ct = default) =>
        Result.Success(await NotificationSettingsReader.ReadAsync(_store, ct).ConfigureAwait(false));
}
