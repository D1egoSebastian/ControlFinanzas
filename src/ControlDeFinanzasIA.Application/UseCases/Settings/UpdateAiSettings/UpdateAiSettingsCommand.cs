using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;

/// <summary>
/// Comando con los seis campos de configuración de IA que persiste
/// <see cref="UpdateAiSettingsUseCase"/> (F-012, R-5, D-9). Persistencia
/// pura: ningún campo dispara una llamada de red.
/// </summary>
public sealed record UpdateAiSettingsCommand(
    AiProvider Provider,
    string ApiKey,
    string Model,
    string? BaseUrl,
    double Temperature,
    int MaxTokensPerRequest);
