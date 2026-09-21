using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;

/// <summary>
/// Use case que persiste la configuración de IA (F-012, R-5, D-9).
/// Persistencia pura: el constructor NO recibe <c>HttpClient</c>,
/// <c>IHttpClientFactory</c> ni ningún tipo <c>ILLMClient</c> — esa
/// firma ES la garantía de "cero llamadas de red" de esta fase, y es
/// asserteable por test.
/// </summary>
public sealed class UpdateAiSettingsUseCase
{
    private readonly ISettingsStore _store;
    private readonly IValidator<UpdateAiSettingsCommand> _validator;
    private readonly ILogger<UpdateAiSettingsUseCase> _logger;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public UpdateAiSettingsUseCase(
        ISettingsStore store,
        IValidator<UpdateAiSettingsCommand> validator,
        ILogger<UpdateAiSettingsUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Valida y persiste los seis campos de configuración de IA.
    /// </summary>
    /// <returns>
    /// <see cref="Result"/> exitoso si los seis campos se persisten;
    /// <see cref="Error"/> código <c>"Validation"</c> si el comando no es
    /// válido, o el primer <see cref="Error"/> no exitoso devuelto por
    /// <see cref="ISettingsStore.SetAsync{T}"/> si una escritura falla.
    /// Todas las validaciones corren antes de la primera escritura
    /// (validate-all-then-write-all), por lo que el único fallo parcial
    /// alcanzable es un error de I/O genuino en disco (D-5).
    /// </returns>
    public async Task<Result> ExecuteAsync(UpdateAiSettingsCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        FluentValidation.Results.ValidationResult validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            IReadOnlyDictionary<string, string[]> details = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            _logger.LogWarning("Invalid AI settings command.");
            return Result.Failure(
                "Validation",
                "La configuración de IA no es válida.",
                details);
        }

        string? normalizedBaseUrl = string.IsNullOrWhiteSpace(command.BaseUrl) ? null : command.BaseUrl;

        Result providerResult = await _store.SetAsync(SettingKeys.AiProvider, command.Provider, ct).ConfigureAwait(false);
        if (!providerResult.IsSuccess)
        {
            return providerResult;
        }

        Result apiKeyResult = await _store.SetAsync(SettingKeys.AiApiKey, command.ApiKey, ct).ConfigureAwait(false);
        if (!apiKeyResult.IsSuccess)
        {
            return apiKeyResult;
        }

        Result modelResult = await _store.SetAsync(SettingKeys.AiModel, command.Model, ct).ConfigureAwait(false);
        if (!modelResult.IsSuccess)
        {
            return modelResult;
        }

        Result baseUrlResult = await _store.SetAsync(SettingKeys.AiBaseUrl, normalizedBaseUrl, ct).ConfigureAwait(false);
        if (!baseUrlResult.IsSuccess)
        {
            return baseUrlResult;
        }

        Result temperatureResult = await _store.SetAsync(SettingKeys.AiTemperature, command.Temperature, ct).ConfigureAwait(false);
        if (!temperatureResult.IsSuccess)
        {
            return temperatureResult;
        }

        Result maxTokensResult = await _store.SetAsync(SettingKeys.AiMaxTokensPerRequest, command.MaxTokensPerRequest, ct).ConfigureAwait(false);
        if (!maxTokensResult.IsSuccess)
        {
            return maxTokensResult;
        }

        return Result.Success(null!);
    }
}
