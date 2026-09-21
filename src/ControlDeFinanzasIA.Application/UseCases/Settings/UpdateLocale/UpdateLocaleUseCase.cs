using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Settings.UpdateLocale;

/// <summary>
/// Use case que persiste la preferencia de idioma (F-012, R-4, D-4,
/// D-9). El cambio aplica al reiniciar la app, no en caliente (Q2).
/// </summary>
public sealed class UpdateLocaleUseCase
{
    private readonly ISettingsStore _store;
    private readonly ILogger<UpdateLocaleUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public UpdateLocaleUseCase(ISettingsStore store, ILogger<UpdateLocaleUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Persiste la preferencia de idioma.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> exitoso si se persiste; <see cref="Error"/>
    /// código <c>"UnsupportedLocale"</c> si <paramref name="locale"/> no
    /// está en <see cref="SupportedLocales.All"/> — guardia primaria de
    /// las tres capas D-4 (la lista blanca protege el arranque).
    /// </returns>
    public Task<Result> ExecuteAsync(string locale, CancellationToken ct = default)
    {
        if (!SupportedLocales.IsSupported(locale))
        {
            _logger.LogWarning("Unsupported locale: {Locale}.", locale);
            return Task.FromResult(Result.Failure("UnsupportedLocale", $"El idioma '{locale}' no está soportado."));
        }

        return _store.SetAsync(SettingKeys.Locale, locale, ct);
    }
}
