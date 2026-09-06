namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Abstracción de "seam" para acceder a la hora actual sin acoplarse
/// a <see cref="DateTime.Now"/>. Toda la aplicación (use cases,
/// entidades que registran timestamps, etc.) recibe
/// <see cref="IClock"/> por inyección de dependencias y llama a
/// <see cref="UtcNow"/> o <see cref="Today"/>.
/// </summary>
/// <remarks>
/// Las implementaciones reales (<c>SystemClock</c> en F-008)
/// deben garantizar que <see cref="UtcNow"/> retorna un
/// <see cref="DateTime"/> con <see cref="DateTimeKind.Utc"/>.
/// Los fakes (<c>FakeClock</c> en
/// <c>tests/.../TestDoubles/FakeClock.cs</c>) NO fuerzan este
/// <see cref="DateTimeKind"/>: el fake almacena exactamente lo que
/// se le setea, para permitir que los tests verifiquen el contrato
/// del fake, no la semántica de UTC (que es responsabilidad de la
/// implementación real).
/// </remarks>
public interface IClock
{
    /// <summary>
    /// Retorna la hora actual en UTC.
    /// </summary>
    /// <remarks>
    /// Las implementaciones reales deben garantizar que el
    /// <see cref="DateTime"/> retornado tiene
    /// <see cref="DateTimeKind.Utc"/>.
    /// </remarks>
    DateTime UtcNow { get; }

    /// <summary>
    /// Retorna la fecha actual.
    /// </summary>
    /// <remarks>
    /// La definición de "actual" (zona horaria, momento del día) es
    /// decisión de la implementación. La implementación real
    /// probablemente usa la zona local del usuario, pero la interfaz
    /// no lo garantiza.
    /// </remarks>
    DateOnly Today { get; }
}
