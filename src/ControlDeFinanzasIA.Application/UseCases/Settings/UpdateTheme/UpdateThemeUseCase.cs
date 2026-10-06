using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Settings.UpdateTheme;

/// <summary>
/// Use case que persiste la preferencia de tema (F-012, R-3, D-9).
/// Solo persiste — la aplicación real del tema a
/// <c>RequestedThemeVariant</c> no ocurre en esta capa: la hace la UI mediante
/// <c>IThemeService</c> (D-8).
/// </summary>
public sealed class UpdateThemeUseCase
{
    private readonly ISettingsStore _store;
    private readonly ILogger<UpdateThemeUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public UpdateThemeUseCase(ISettingsStore store, ILogger<UpdateThemeUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Persiste la preferencia de tema.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> exitoso si se persiste; <see cref="Error"/>
    /// código <c>"Validation"</c> si <paramref name="theme"/> no es un
    /// valor definido de <see cref="AppTheme"/>.
    /// </returns>
    public Task<Result> ExecuteAsync(AppTheme theme, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(theme))
        {
            _logger.LogWarning("Invalid AppTheme value: {Theme}.", theme);
            return Task.FromResult(Result.Failure("Validation", $"El tema '{theme}' no es válido."));
        }

        return _store.SetAsync(SettingKeys.Theme, theme, ct);
    }
}
