using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Implementación de prueba de <see cref="IPathResolver"/> que
/// permite a los tests controlar las rutas explícitamente.
/// </summary>
/// <remarks>
/// Esta clase NO valida las rutas. Almacena exactamente lo que se
/// le setea, incluyendo strings vacíos. Esto es intencional — el
/// fake es un test double, no una validación de negocio.
/// </remarks>
public class FakePathResolver : IPathResolver
{
    public string DataDirectory { get; set; } = string.Empty;
    public string DatabaseFilePath { get; set; } = string.Empty;
}
