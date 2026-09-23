using ControlDeFinanzasIA.Application.UseCases.Dashboard.Common;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.GetAnomalyNarrative;

/// <summary>
/// Resultado de <see cref="GetAnomalyNarrativeUseCase.ExecuteAsync"/>
/// (F-014, R-5, R-6, D-8, D-9). Success-shaped, igual que
/// <c>InsightsNarrative</c>.
/// </summary>
/// <param name="Status">Estado de la narracion por lotes.</param>
/// <param name="MessagesByAlertId">
/// Texto narrado por <c>AlertId</c>, solo para las anomalias que el parser
/// pudo resolver (D-9). Un id ausente significa "mantener la plantilla
/// deterministica" — nunca implica un fallo del lote completo (spec S-6).
/// </param>
public sealed record AnomalyNarration(
    NarrativeStatus Status,
    IReadOnlyDictionary<Guid, string> MessagesByAlertId);
