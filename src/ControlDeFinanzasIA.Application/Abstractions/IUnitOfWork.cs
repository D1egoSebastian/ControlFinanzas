using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Unidad de trabajo que coordina operaciones de múltiples
/// repositorios en una sola transacción atómica.
/// </summary>
/// <remarks>
/// La implementación (en F-002 de Fase 2) envuelve
/// <c>SaveChangesAsync</c> de EF Core. Es trivial: un wrapper
/// que llama a <c>DbContext.SaveChangesAsync</c>. El rol real de
/// esta interface es: (1) permitir a los use cases coordinar
/// varios repos en una sola transacción, y (2) abstraer la
/// persistencia de la lógica de negocio.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Persiste los cambios pendientes en el store atómicamente.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Número de filas afectadas (útil para verificar que la
    /// operación tuvo efecto, o 0 si no había cambios pendientes).
    /// </returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
