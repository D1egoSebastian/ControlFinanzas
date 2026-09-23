using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetInsights;

/// <summary>
/// Resultado de <see cref="GetInsightsUseCase.ExecuteAsync"/> (F-014, R-4,
/// D-8). Success-shaped: nunca hay un <c>Result.Failure</c> por una razon
/// de IA — <see cref="Status"/> es lo unico que distingue "texto generado"
/// de "no configurada" / "no disponible" / "nada que narrar".
/// </summary>
/// <param name="Status">Estado de la narracion.</param>
/// <param name="Text">
/// Texto narrado por el modelo (2-4 oraciones), o <see langword="null"/>
/// cuando <paramref name="Status"/> no es <see cref="NarrativeStatus.Generated"/>.
/// </param>
public sealed record InsightsNarrative(NarrativeStatus Status, string? Text);
