using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Fachada tipada sobre <see cref="ISettingRepository"/> (CRUD crudo,
/// sin cambios — F-012 Q3). Los use cases dependen solo de esta
/// interfaz, nunca directamente de <see cref="ISettingRepository"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Valor crudo de la clave, o <paramref name="defaultValue"/> si no existe.</summary>
    Task<string?> GetStringAsync(string key, string? defaultValue = null, CancellationToken ct = default);

    /// <summary>
    /// Enum parseado por NOMBRE (case-insensitive). Un valor invalido o
    /// ausente resuelve a <paramref name="defaultValue"/>; nunca lanza.
    /// </summary>
    Task<T> GetEnumAsync<T>(string key, T defaultValue, CancellationToken ct = default) where T : struct, Enum;

    /// <summary>
    /// Hace upsert por clave. Serializa siempre en
    /// <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
    /// </summary>
    Task<Result> SetAsync<T>(string key, T value, CancellationToken ct = default);

    /// <summary>
    /// Modelo de lectura completo. NUNCA <see langword="null"/>, NUNCA
    /// lanza; una sola ida a la base de datos.
    /// </summary>
    Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default);
}
