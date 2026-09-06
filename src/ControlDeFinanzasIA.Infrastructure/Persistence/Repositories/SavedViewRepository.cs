using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación concreta de <see cref="ISavedViewRepository"/> basada en
/// Entity Framework Core y el <see cref="AppDbContext"/> del proyecto.
/// </summary>
/// <remarks>
/// Implementa las reglas de negocio definidas en la especificación F-006
/// de la Fase 4: unicidad de <c>Name</c>, existencia de la entidad y
/// filtrado por <c>EntityType</c>.
/// </remarks>
public sealed class SavedViewRepository : ISavedViewRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="SavedViewRepository"/>.
    /// </summary>
    /// <param name="db">Contexto de EF Core utilizado para persistir las vistas.</param>
    public SavedViewRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Añade una nueva <see cref="SavedView"/> a la base de datos.
    /// </summary>
    /// <remarks>
    /// Valida que el nombre sea único. Si ya existe, devuelve
    /// <c>Result.Failure("DuplicateName", ...)</c>. También captura
    /// <see cref="DbUpdateException"/> para mitigar race conditions
    /// concurrentes sobre el índice único de la base de datos.
    /// </remarks>
    /// <param name="savedView">Vista a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se persistió; de lo contrario,
    /// un error con código <c>DuplicateName</c>.
    /// </returns>
    public async Task<Result> AddAsync(SavedView savedView, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(savedView);

        if (await _db.SavedViews.AnyAsync(s => s.Name == savedView.Name, ct))
        {
            return Result.Failure(
                "DuplicateName",
                $"Ya existe una vista guardada con el nombre '{savedView.Name}'.");
        }

        try
        {
            _db.SavedViews.Add(savedView);
            await _db.SaveChangesAsync(ct);
            return Result.Success(null!);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Result.Failure(
                "DuplicateName",
                $"Ya existe una vista guardada con el nombre '{savedView.Name}'.");
        }
    }

    /// <summary>
    /// Actualiza una <see cref="SavedView"/> existente.
    /// </summary>
    /// <remarks>
    /// Valida que la entidad exista; si no, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>. Utiliza el método
    /// <see cref="SavedView.Update"/> para mutar la vista trackeada.
    /// </remarks>
    /// <param name="savedView">Vista con los valores actualizados.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se actualizó; de lo contrario,
    /// un error con código <c>NotFound</c>.
    /// </returns>
    public async Task<Result> UpdateAsync(SavedView savedView, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(savedView);

        SavedView? existing = await _db.SavedViews.FindAsync(new object?[] { savedView.Id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Vista guardada {savedView.Id} no encontrada.");
        }

        existing.Update(savedView.Name, savedView.FilterJson, savedView.UpdatedAt);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Elimina una <see cref="SavedView"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Valida que la entidad exista; si no, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>.
    /// </remarks>
    /// <param name="id">Identificador de la vista a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se eliminó; de lo contrario,
    /// un error con código <c>NotFound</c>.
    /// </returns>
    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(
                "NotFound",
                $"Vista guardada {id} no encontrada.");
        }

        SavedView? existing = await _db.SavedViews.FindAsync(new object?[] { id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Vista guardada {id} no encontrada.");
        }

        _db.SavedViews.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Obtiene una <see cref="SavedView"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Devuelve <see langword="null"/> si la entidad no existe.
    /// </remarks>
    /// <param name="id">Identificador de la vista.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// La vista encontrada, o <see langword="null"/> si no existe.
    /// </returns>
    public async Task<SavedView?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _db.SavedViews.FindAsync(new object?[] { id }, ct);
    }

    /// <summary>
    /// Obtiene todas las <see cref="SavedView"/> ordenadas alfabéticamente por nombre.
    /// </summary>
    /// <remarks>
    /// La consulta utiliza <c>AsNoTracking</c> porque es de solo lectura.
    /// </remarks>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Colección de solo lectura con todas las vistas ordenadas por <c>Name</c>.
    /// </returns>
    public async Task<IReadOnlyList<SavedView>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.SavedViews
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Obtiene las <see cref="SavedView"/> de un tipo de entidad dado,
    /// ordenadas alfabéticamente por nombre.
    /// </summary>
    /// <remarks>
    /// La consulta utiliza <c>AsNoTracking</c> porque es de solo lectura.
    /// </remarks>
    /// <param name="entityType">Tipo de entidad (gastos o ingresos).</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Colección de solo lectura con las vistas del tipo solicitado.
    /// </returns>
    public async Task<IReadOnlyList<SavedView>> GetByEntityTypeAsync(
        TransactionType entityType,
        CancellationToken ct = default)
    {
        return await _db.SavedViews
            .AsNoTracking()
            .Where(s => s.EntityType == entityType)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // SQLite returns SQLITE_CONSTRAINT (19) for unique constraint violations.
        return ex.InnerException is SqliteException sqliteEx
            && sqliteEx.SqliteErrorCode == 19;
    }
}
