using System.Collections.Concurrent;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;

/// <summary>
/// Caché de narración del dashboard, en memoria, solo de la sesión de
/// proceso actual (F-014, slice 5, D-11). Registrada Singleton (los casos
/// de uso que la consumen son transient). NO es una caché durable — R-9
/// prohíbe explícitamente cache tables o persistencia de esta caché.
/// </summary>
/// <remarks>
/// La clave recomendada (ver <see cref="BuildKey"/>) es
/// <c>promptName + locale + provider + model + renderedUserPrompt</c>. El
/// prompt de usuario ya renderizado contiene el mes y cada cifra, así que
/// ES el "(month, input-figure fingerprint)" de D-12 sin código de huella
/// dedicado y sin riesgo de colisión. Locale/provider/model se agregan
/// porque un cambio de idioma o de modelo no debe servir prosa obsoleta.
/// </remarks>
public sealed class DashboardNarrationCache
{
    /// <summary>Tamaño máximo antes de que un <see cref="Set{T}"/> adicional limpie toda la caché.</summary>
    public const int Capacity = 32;

    private readonly ConcurrentDictionary<string, object> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Construye la clave de caché a partir de los componentes de D-11.
    /// Usa el separador de control <c>\u001F</c> (Unit Separator) para que
    /// ningún componente pueda producir una colisión por concatenación.
    /// </summary>
    public static string BuildKey(string promptName, string locale, string aiProvider, string aiModel, string renderedUserPrompt) =>
        string.Join('\u001F', promptName, locale, aiProvider, aiModel, renderedUserPrompt);

    /// <summary>Intenta obtener un valor cacheado del tipo esperado. No lanza en caso de tipo incorrecto.</summary>
    public bool TryGetValue<T>(string key, out T value)
    {
        if (_entries.TryGetValue(key, out object? cached) && cached is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Guarda un valor SOLO si <paramref name="status"/> es
    /// <see cref="NarrativeStatus.Generated"/> (D-11) — cachear un fallback
    /// fijaría "IA no disponible" para el resto de la sesión tras un
    /// timeout transitorio. En overflow (al llegar a <see cref="Capacity"/>)
    /// limpia toda la caché antes de guardar la nueva entrada; una sesión
    /// mantiene apenas un puñado de meses, así que un esquema LRU sería
    /// ceremonia innecesaria.
    /// </summary>
    public void Set<T>(string key, T value, NarrativeStatus status)
        where T : notnull
    {
        if (status != NarrativeStatus.Generated)
        {
            return;
        }

        if (_entries.Count >= Capacity)
        {
            _entries.Clear();
        }

        _entries[key] = value;
    }
}
