using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación concreta de <see cref="ITagRepository"/> basada en
/// Entity Framework Core y el <see cref="AppDbContext"/> del proyecto.
/// </summary>
/// <remarks>
/// Implementa las reglas de negocio definidas en la especificación F-004
/// de la Fase 4: unicidad de <c>Name</c> y existencia de la entidad.
/// </remarks>
public sealed class TagRepository : ITagRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="TagRepository"/>.
    /// </summary>
    /// <param name="db">Contexto de EF Core utilizado para persistir los tags.</param>
    public TagRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Añade un nuevo <see cref="Tag"/> a la base de datos.
    /// </summary>
    /// <remarks>
    /// Valida que el nombre sea único. Si ya existe, devuelve
    /// <c>Result.Failure("DuplicateName", ...)</c>. También captura
    /// <see cref="DbUpdateException"/> para mitigar race conditions
    /// concurrentes sobre el índice único de la base de datos.
    /// </remarks>
    /// <param name="tag">Tag a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se persistió; de lo contrario,
    /// un error con código <c>DuplicateName</c>.
    /// </returns>
    public async Task<Result> AddAsync(Tag tag, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (await _db.Tags.AnyAsync(t => t.Name == tag.Name, ct))
        {
            return Result.Failure(
                "DuplicateName",
                $"Ya existe un tag con el nombre '{tag.Name}'.");
        }

        try
        {
            _db.Tags.Add(tag);
            await _db.SaveChangesAsync(ct);
            return Result.Success(null!);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Result.Failure(
                "DuplicateName",
                $"Ya existe un tag con el nombre '{tag.Name}'.");
        }
    }

    /// <summary>
    /// Actualiza un <see cref="Tag"/> existente.
    /// </summary>
    /// <remarks>
    /// Valida que la entidad exista; si no, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>. Utiliza el método
    /// <see cref="Tag.Update"/> para mutar el tag trackeado.
    /// </remarks>
    /// <param name="tag">Tag con los valores actualizados.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se actualizó; de lo contrario,
    /// un error con código <c>NotFound</c>.
    /// </returns>
    public async Task<Result> UpdateAsync(Tag tag, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tag);

        Tag? existing = await _db.Tags.FindAsync(new object?[] { tag.Id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Tag {tag.Id} no encontrado.");
        }

        existing.Update(tag.Name, tag.Color);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Elimina un <see cref="Tag"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Valida que la entidad exista; si no, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>. Las filas de la tabla
    /// <c>ExpenseTags</c> asociadas se eliminan por la cascada definida
    /// en <see cref="ExpenseConfiguration"/>.
    /// </remarks>
    /// <param name="id">Identificador del tag a eliminar.</param>
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
                $"Tag {id} no encontrado.");
        }

        Tag? existing = await _db.Tags.FindAsync(new object?[] { id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Tag {id} no encontrado.");
        }

        _db.Tags.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Obtiene un <see cref="Tag"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Devuelve <see langword="null"/> si la entidad no existe.
    /// </remarks>
    /// <param name="id">Identificador del tag.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// El tag encontrado, o <see langword="null"/> si no existe.
    /// </returns>
    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _db.Tags.FindAsync(new object?[] { id }, ct);
    }

    /// <summary>
    /// Obtiene todos los <see cref="Tag"/> ordenados alfabéticamente por nombre.
    /// </summary>
    /// <remarks>
    /// La consulta utiliza <c>AsNoTracking</c> porque es de solo lectura.
    /// </remarks>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Colección de solo lectura con todos los tags ordenados por <c>Name</c>.
    /// </returns>
    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // SQLite returns SQLITE_CONSTRAINT (19) for unique constraint violations.
        return ex.InnerException is Microsoft.Data.Sqlite.SqliteException sqliteEx
            && sqliteEx.SqliteErrorCode == 19;
    }
}
