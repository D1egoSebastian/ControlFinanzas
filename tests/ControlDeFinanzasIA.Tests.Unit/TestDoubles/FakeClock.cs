using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Implementación de prueba de <see cref="IClock"/> que permite a
/// los tests controlar <see cref="UtcNow"/> y <see cref="Today"/>
/// explícitamente.
/// </summary>
/// <remarks>
/// Esta clase NO fuerza <see cref="DateTimeKind.Utc"/> en
/// <see cref="UtcNow"/>: almacena exactamente lo que se le setea.
/// Esto es intencional — el fake verifica su propio contrato
/// (almacenar y retornar), no la semántica de UTC (que es
/// responsabilidad de la implementación real en F-008).
/// </remarks>
public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; }
    public DateOnly Today { get; set; }
}
