using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación concreta de <see cref="ICategoryRepository"/> basada en
/// Entity Framework Core y el <see cref="AppDbContext"/> del proyecto.
/// </summary>
/// <remarks>
/// Implementa las reglas de negocio RN-1 (unicidad de <c>Name</c>),
/// RN-2 (inmutabilidad de categorías del sistema) y RN-3 (entidad no
/// encontrada) definidas en la especificación F-004 de la Fase 2.
/// </remarks>
public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="CategoryRepository"/>.
    /// </summary>
    /// <param name="db">Contexto de EF Core utilizado para persistir las categorías.</param>
    public CategoryRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Añade una nueva <see cref="Category"/> a la base de datos.
    /// </summary>
    /// <remarks>
    /// Valida RN-1: el nombre debe ser único. Si ya existe, devuelve
    /// <c>Result.Failure("DuplicateName", ...)</c>. También captura
    /// <see cref="DbUpdateException"/> para mitigar race conditions
    /// concurrentes sobre el índice único de la base de datos.
    /// </remarks>
    /// <param name="category">Categoría a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se persistió; de lo contrario,
    /// un error con código <c>DuplicateName</c>.
    /// </returns>
    public async Task<Result> AddAsync(Category category, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (await _db.Categories.AnyAsync(c => c.Name == category.Name, ct))
        {
            return Result.Failure(
                "DuplicateName",
                $"Ya existe una categoría con el nombre '{category.Name}'.");
        }

        try
        {
            _db.Categories.Add(category);
            await _db.SaveChangesAsync(ct);
            return Result.Success(null!);
        }
        catch (DbUpdateException)
        {
            return Result.Failure(
                "DuplicateName",
                $"Ya existe una categoría con el nombre '{category.Name}'.");
        }
    }

    /// <summary>
    /// Actualiza una <see cref="Category"/> existente.
    /// </summary>
    /// <remarks>
    /// Valida RN-3: la entidad debe existir; si no, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>. Valida RN-2: no permite
    /// renombrar una categoría del sistema (<c>IsSystem = true</c>).
    /// </remarks>
    /// <param name="category">Categoría con los valores actualizados.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se actualizó; de lo contrario,
    /// un error con código <c>NotFound</c> o <c>SystemCategoryImmutable</c>.
    /// </returns>
    public async Task<Result> UpdateAsync(Category category, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        Category? existing = await _db.Categories.FindAsync(new object?[] { category.Id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Categoría {category.Id} no encontrada.");
        }

        if (existing.IsSystem && existing.Name != category.Name)
        {
            return Result.Failure(
                "SystemCategoryImmutable",
                "No se puede renombrar una categoría del sistema.");
        }

        _db.Entry(existing).CurrentValues.SetValues(category);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Elimina una <see cref="Category"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Valida RN-3: la entidad debe existir; si no, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>. Valida RN-2: no permite
    /// eliminar una categoría del sistema (<c>IsSystem = true</c>).
    /// </remarks>
    /// <param name="id">Identificador de la categoría a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se eliminó; de lo contrario,
    /// un error con código <c>NotFound</c> o <c>SystemCategoryImmutable</c>.
    /// </returns>
    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(
                "NotFound",
                $"Categoría {id} no encontrada.");
        }

        Category? existing = await _db.Categories.FindAsync(new object?[] { id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Categoría {id} no encontrada.");
        }

        if (existing.IsSystem)
        {
            return Result.Failure(
                "SystemCategoryImmutable",
                "No se puede eliminar una categoría del sistema.");
        }

        _db.Categories.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Obtiene una <see cref="Category"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Implementa RN-3: devuelve <see langword="null"/> si la entidad no existe.
    /// </remarks>
    /// <param name="id">Identificador de la categoría.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// La categoría encontrada, o <see langword="null"/> si no existe.
    /// </returns>
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _db.Categories.FindAsync(new object?[] { id }, ct);
    }

    /// <summary>
    /// Obtiene todas las <see cref="Category"/> ordenadas alfabéticamente por nombre.
    /// </summary>
    /// <remarks>
    /// La consulta utiliza <c>AsNoTracking</c> porque es de solo lectura.
    /// </remarks>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Colección de solo lectura con todas las categorías ordenadas por <c>Name</c>.
    /// </returns>
    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }
}
