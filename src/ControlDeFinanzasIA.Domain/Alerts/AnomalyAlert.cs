namespace ControlDeFinanzasIA.Domain.Alerts;

/// <summary>
/// Alerta de anomalía. La IA (en F-006+ de Fase 2+) detecta
/// transacciones inusuales y crea alertas. El usuario puede
/// revisarlas y marcarlas como acknowledged.
/// </summary>
public sealed record AnomalyAlert
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public Guid TransactionId { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = "Warning";
    public DateTime? AcknowledgedAt { get; init; }
}
