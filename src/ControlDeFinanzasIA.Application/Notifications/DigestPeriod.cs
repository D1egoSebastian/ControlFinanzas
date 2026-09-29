using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.Notifications;

/// <summary>
/// Cálculo puro de los límites del resumen periódico (F-017, R-13, D-12).
/// Sin dependencia de <c>ISOWeek</c>: el lunes de la semana ISO se obtiene
/// por aritmética de <see cref="DayOfWeek"/>, así que no hay caso especial
/// en el cambio de año.
/// </summary>
public static class DigestPeriod
{
    /// <summary>Días adicionales a <c>today</c> que cubre la ventana semanal (<c>[today, today + 6]</c>).</summary>
    public const int WeeklyWindowExtraDays = 6;

    /// <summary>
    /// Primer día del período que contiene a <paramref name="today"/>:
    /// el propio día para <see cref="DigestFrequency.Daily"/>, el lunes de
    /// la semana ISO para <see cref="DigestFrequency.Weekly"/>.
    /// </summary>
    /// <param name="frequency">Frecuencia configurada; <see cref="DigestFrequency.Off"/> no tiene período.</param>
    /// <param name="today">Fecha de referencia de la corrida.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="frequency"/> es <see cref="DigestFrequency.Off"/> o no está definida.</exception>
    public static DateOnly Start(DigestFrequency frequency, DateOnly today) => frequency switch
    {
        DigestFrequency.Daily => today,
        DigestFrequency.Weekly => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Digest frequency has no period."),
    };

    /// <summary>
    /// Último día (inclusivo) de la ventana de pagos del resumen: solo hacia
    /// adelante desde <paramref name="today"/> — <c>today</c> para
    /// <see cref="DigestFrequency.Daily"/>, <c>today + 6</c> para
    /// <see cref="DigestFrequency.Weekly"/>. Lo vencido queda fuera (D-12).
    /// </summary>
    /// <param name="frequency">Frecuencia configurada; <see cref="DigestFrequency.Off"/> no tiene ventana.</param>
    /// <param name="today">Fecha de referencia de la corrida.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="frequency"/> es <see cref="DigestFrequency.Off"/> o no está definida.</exception>
    public static DateOnly WindowEnd(DigestFrequency frequency, DateOnly today) => frequency switch
    {
        DigestFrequency.Daily => today,
        DigestFrequency.Weekly => today.AddDays(WeeklyWindowExtraDays),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Digest frequency has no window."),
    };
}
