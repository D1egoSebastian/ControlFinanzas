using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Fake de <see cref="ISettingsStore"/> respaldado por un mapa clave-valor
/// real en memoria (F-017, D-7, D-15). A diferencia de
/// <see cref="FakeSettingsStore"/> (que siempre retorna el default), este
/// doble hace round-trip de verdad: lo que se escribe con
/// <see cref="SetAsync{T}"/> es lo que <see cref="GetStringAsync"/> y
/// <see cref="GetEnumAsync{T}"/> devuelven después. Serializa igual que
/// <c>SettingsStore.Serialize</c> (bool en minúsculas, enum por nombre).
/// </summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the <see cref="AppSettings"/> returned by <see cref="GetAppSettingsAsync"/>.</summary>
    public AppSettings AppSettings { get; set; } = AppSettings.Defaults;

    /// <summary>Seeds a raw string value for <paramref name="key"/>, as if it had been persisted earlier.</summary>
    public void Seed(string key, string value) => _values[key] = value;

    /// <inheritdoc />
    public Task<string?> GetStringAsync(string key, string? defaultValue = null, CancellationToken ct = default) =>
        Task.FromResult(_values.TryGetValue(key, out string? value) ? value : defaultValue);

    /// <inheritdoc />
    public Task<T> GetEnumAsync<T>(string key, T defaultValue, CancellationToken ct = default)
        where T : struct, Enum
    {
        if (_values.TryGetValue(key, out string? raw)
            && Enum.TryParse(raw, ignoreCase: true, out T parsed)
            && Enum.IsDefined(parsed))
        {
            return Task.FromResult(parsed);
        }

        return Task.FromResult(defaultValue);
    }

    /// <inheritdoc />
    public Task<Result> SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        _values[key] = Serialize(value);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default) =>
        Task.FromResult(AppSettings);

    private static string Serialize<T>(T value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        Enum e => e.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
