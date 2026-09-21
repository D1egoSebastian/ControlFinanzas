using System.Globalization;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Infrastructure.Settings;

/// <summary>
/// Implementacion de <see cref="ISettingsStore"/> sobre
/// <see cref="ISettingRepository"/>. Sin <c>AppDbContext</c>, sin
/// <c>HttpClient</c>, sin <c>IHttpClientFactory</c> — la firma del
/// constructor ES la garantia de "cero llamadas de IA" de esta fase, y
/// es verificable por reflexion (F-012 D-5).
/// </summary>
public sealed class SettingsStore : ISettingsStore
{
    private readonly ISettingRepository _repository;
    private readonly ILogger<SettingsStore> _logger;

    public SettingsStore(ISettingRepository repository, ILogger<SettingsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _logger = logger;
    }

    public async Task<string?> GetStringAsync(string key, string? defaultValue = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Dictionary<string, string> map = await LoadMapAsync(ct).ConfigureAwait(false);
        return map.TryGetValue(key, out string? value) ? value : defaultValue;
    }

    public async Task<T> GetEnumAsync<T>(string key, T defaultValue, CancellationToken ct = default) where T : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Dictionary<string, string> map = await LoadMapAsync(ct).ConfigureAwait(false);
        return ParseEnum(map, key, defaultValue);
    }

    public async Task<Result> SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string serialized = Serialize(value);
        IReadOnlyList<Setting> rows = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        Setting? existing = rows.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

        return existing is null
            ? await _repository.AddAsync(new Setting { Id = Guid.NewGuid(), Key = key, Value = serialized }, ct).ConfigureAwait(false)
            : await _repository.UpdateAsync(existing with { Value = serialized }, ct).ConfigureAwait(false);
    }

    public async Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default)
    {
        try
        {
            Dictionary<string, string> map = await LoadMapAsync(ct).ConfigureAwait(false);

            string locale = map.GetValueOrDefault(SettingKeys.Locale) ?? string.Empty;

            return new AppSettings(
                ParseEnum(map, SettingKeys.Theme, AppSettings.DefaultTheme),
                SupportedLocales.IsSupported(locale) ? locale : SupportedLocales.Default,
                ParseEnum(map, SettingKeys.AiProvider, AppSettings.DefaultAiProvider),
                map.GetValueOrDefault(SettingKeys.AiApiKey) ?? string.Empty,
                map.GetValueOrDefault(SettingKeys.AiModel) ?? string.Empty,
                NullIfEmpty(map.GetValueOrDefault(SettingKeys.AiBaseUrl)),
                ParseDouble(map, SettingKeys.AiTemperature, AppSettings.DefaultAiTemperature),
                ParseInt(map, SettingKeys.AiMaxTokensPerRequest, AppSettings.DefaultAiMaxTokensPerRequest));
        }
        catch (Exception ex) // deliberadamente amplio: el arranque debe degradar, nunca bloquearse (proposal R-8)
        {
            _logger.LogWarning(ex, "Settings could not be read; falling back to defaults.");
            return AppSettings.Defaults;
        }
    }

    private async Task<Dictionary<string, string>> LoadMapAsync(CancellationToken ct)
    {
        IReadOnlyList<Setting> rows = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        return rows
            .GroupBy(s => s.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
    }

    private static T ParseEnum<T>(Dictionary<string, string> map, string key, T defaultValue) where T : struct, Enum
    {
        if (map.TryGetValue(key, out string? raw)
            && Enum.TryParse(raw, ignoreCase: true, out T parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static double ParseDouble(Dictionary<string, string> map, string key, double defaultValue)
    {
        if (map.TryGetValue(key, out string? raw)
            && double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static int ParseInt(Dictionary<string, string> map, string key, int defaultValue)
    {
        if (map.TryGetValue(key, out string? raw)
            && int.TryParse(raw, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string Serialize<T>(T value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        Enum e => e.ToString(), // NOMBRE, no ordinal
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
