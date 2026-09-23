using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Alerts;

namespace ControlDeFinanzasIA.Application.UseCases.Dashboard.AcknowledgeAnomaly;

/// <summary>
/// Use case que reconoce (descarta) una <see cref="AnomalyAlert"/> (F-014,
/// R-3, D-7). Es la unica accion de descarte permitida por el alcance de
/// esta fase (R-9): descarte por fila, sin pantalla dedicada de anomalias.
/// </summary>
public sealed class AcknowledgeAnomalyUseCase
{
    private readonly IAnomalyAlertRepository _anomalyAlertRepository;
    private readonly IClock _clock;

    /// <summary>
    /// Inicializa una nueva instancia del use case.
    /// </summary>
    public AcknowledgeAnomalyUseCase(IAnomalyAlertRepository anomalyAlertRepository, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(anomalyAlertRepository);
        ArgumentNullException.ThrowIfNull(clock);

        _anomalyAlertRepository = anomalyAlertRepository;
        _clock = clock;
    }

    /// <summary>
    /// Reconoce la alerta identificada por <paramref name="alertId"/>.
    /// Idempotente: reconocer una alerta ya reconocida es un no-op exitoso
    /// que conserva el timestamp original (<see cref="AnomalyAlert.Acknowledge"/>).
    /// </summary>
    /// <param name="alertId">Identificador de la alerta a reconocer.</param>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="Result.Success(object)"/> en exito o si ya estaba
    /// reconocida; <see cref="Result.Failure(string, string)"/> con codigo
    /// <c>"NotFound"</c> si <paramref name="alertId"/> no existe.
    /// </returns>
    public async Task<Result> ExecuteAsync(Guid alertId, CancellationToken ct = default)
    {
        AnomalyAlert? alert = await _anomalyAlertRepository.GetByIdAsync(alertId, ct).ConfigureAwait(false);
        if (alert is null)
        {
            return Result.Failure("NotFound", $"AnomalyAlert {alertId} was not found.");
        }

        if (alert.IsAcknowledged)
        {
            return Result.Success(null!);
        }

        alert.Acknowledge(_clock.UtcNow);
        return await _anomalyAlertRepository.UpdateAsync(alert, ct).ConfigureAwait(false);
    }
}
