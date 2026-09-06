using ControlDeFinanzasIA.Application.Abstractions;

namespace ControlDeFinanzasIA.Infrastructure.Persistence;

/// <summary>
/// Implementación de <see cref="IUnitOfWork"/> basada en Entity Framework
/// Core.
/// </summary>
/// <remarks>
/// <para>
/// La clase está sellada porque su implementación es trivial: delega
/// completamente en <see cref="AppDbContext.SaveChangesAsync(CancellationToken)"/>.
/// No se prevé herencia en v1.
/// </para>
/// <para>
/// Según la especificación de F-001 (RN-3), esta unidad de trabajo es
/// Scoped y comparte el mismo <see cref="AppDbContext"/> que los
/// repositorios concretos.
/// </para>
/// <para>
/// La interfaz <see cref="IUnitOfWork"/> se define en F-006 de la Fase 1.
/// </para>
/// </remarks>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="EfUnitOfWork"/>.
    /// </summary>
    /// <param name="db">Contexto de EF Core compartido.</param>
    public EfUnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Persiste los cambios pendientes en la base de datos.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>Número de filas afectadas.</returns>
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
