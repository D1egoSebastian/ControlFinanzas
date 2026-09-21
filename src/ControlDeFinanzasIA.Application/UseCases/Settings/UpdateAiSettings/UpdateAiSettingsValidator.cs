using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Settings;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Settings.UpdateAiSettings;

/// <summary>
/// Validador FluentValidation para <see cref="UpdateAiSettingsCommand"/>
/// (F-012, R-5, D-2, D-9). Los límites de <see cref="Temperature"/> y
/// <see cref="MaxTokensPerRequest"/> son los pinneados por la nota de
/// reconciliación del spec (2026-09-20): <c>[0.0, 2.0]</c> y
/// <c>[1, 128000]</c>, ambos inclusive.
/// </summary>
public sealed class UpdateAiSettingsValidator : AbstractValidator<UpdateAiSettingsCommand>
{
    /// <summary>
    /// Inicializa una nueva instancia del validador.
    /// </summary>
    public UpdateAiSettingsValidator()
    {
        RuleFor(c => c.Provider)
            .Must(p => Enum.IsDefined(p))
            .WithMessage("El proveedor de IA no es válido.");

        // ApiKey/Model NO llevan NotEmpty: limpiar la clave es una acción legítima.
        RuleFor(c => c.ApiKey)
            .MaximumLength(Setting.MaxValueLength)
            .WithMessage($"La API key no puede superar los {Setting.MaxValueLength} caracteres.");

        RuleFor(c => c.Model)
            .MaximumLength(Setting.MaxValueLength)
            .WithMessage($"El modelo no puede superar los {Setting.MaxValueLength} caracteres.");

        RuleFor(c => c.BaseUrl)
            .Must(BeAnAbsoluteHttpOrHttpsUri)
            .WithMessage("El base URL debe ser una URI absoluta http o https.")
            .When(c => !string.IsNullOrWhiteSpace(c.BaseUrl));

        RuleFor(c => c.Temperature)
            .InclusiveBetween(AppSettings.MinAiTemperature, AppSettings.MaxAiTemperature)
            .WithMessage($"La temperatura debe estar entre {AppSettings.MinAiTemperature} y {AppSettings.MaxAiTemperature}.");

        RuleFor(c => c.MaxTokensPerRequest)
            .InclusiveBetween(AppSettings.MinAiMaxTokensPerRequest, AppSettings.MaxAiMaxTokensPerRequest)
            .WithMessage($"El máximo de tokens debe estar entre {AppSettings.MinAiMaxTokensPerRequest} y {AppSettings.MaxAiMaxTokensPerRequest}.");
    }

    private static bool BeAnAbsoluteHttpOrHttpsUri(string? baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
