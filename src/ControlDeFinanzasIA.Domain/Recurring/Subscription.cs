namespace ControlDeFinanzasIA.Domain.Recurring;

/// <summary>
/// Suscripción. Lista editable de suscripciones, una por servicio.
/// Hereda de <see cref="RecurringRule"/>.
/// </summary>
public sealed record Subscription : RecurringRule
{
}
