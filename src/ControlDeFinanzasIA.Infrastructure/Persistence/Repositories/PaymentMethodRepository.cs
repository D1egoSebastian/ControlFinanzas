using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación concreta de <see cref="IPaymentMethodRepository"/> basada en
/// Entity Framework Core y el <see cref="AppDbContext"/> del proyecto.
/// </summary>
/// <remarks>
/// Implementa las reglas de negocio RN-1 (unicidad de <c>Name</c>),
/// RN-2 (inmutabilidad de métodos de pago del sistema) y RN-3 (entidad no
/// encontrada) definidas en la especificación F-004 de la Fase 2.
/// </remarks>
public sealed class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="PaymentMethodRepository"/>.
    /// </summary>
    /// <param name="db">Contexto de EF Core utilizado para persistir los métodos de pago.</param>
    public PaymentMethodRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Añade un nuevo <see cref="PaymentMethod"/> a la base de datos.
    /// </summary>
    /// <remarks>
    /// Valida RN-1: el nombre debe ser único. Si ya existe, devuelve
    /// <c>Result.Failure("DuplicateName", ...)</c>. También captura
    /// <see cref="DbUpdateException"/> para mitigar race conditions
    /// concurrentes sobre el índice único de la base de datos.
    /// </remarks>
    /// <param name="paymentMethod">Método de pago a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se persistió; de lo contrario,
    /// un error con código <c>DuplicateName</c>.
    /// </returns>
    public async Task<Result> AddAsync(PaymentMethod paymentMethod, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paymentMethod);

        if (await _db.PaymentMethods.AnyAsync(p => p.Name == paymentMethod.Name, ct))
        {
            return Result.Failure(
                "DuplicateName",
                $"Ya existe un método de pago con el nombre '{paymentMethod.Name}'.");
        }

        try
        {
            _db.PaymentMethods.Add(paymentMethod);
            await _db.SaveChangesAsync(ct);
            return Result.Success(null!);
        }
        catch (DbUpdateException)
        {
            return Result.Failure(
                "DuplicateName",
                $"Ya existe un método de pago con el nombre '{paymentMethod.Name}'.");
        }
    }

    /// <summary>
    /// Actualiza un <see cref="PaymentMethod"/> existente.
    /// </summary>
    /// <remarks>
    /// Valida RN-3: la entidad debe existir; si no, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>. Valida RN-2: no permite
    /// renombrar un método de pago del sistema (<c>IsSystem = true</c>).
    /// </remarks>
    /// <param name="paymentMethod">Método de pago con los valores actualizados.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se actualizó; de lo contrario,
    /// un error con código <c>NotFound</c> o <c>SystemPaymentMethodImmutable</c>.
    /// </returns>
    public async Task<Result> UpdateAsync(PaymentMethod paymentMethod, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paymentMethod);

        PaymentMethod? existing = await _db.PaymentMethods.FindAsync(new object?[] { paymentMethod.Id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Método de pago {paymentMethod.Id} no encontrado.");
        }

        if (existing.IsSystem && existing.Name != paymentMethod.Name)
        {
            return Result.Failure(
                "SystemPaymentMethodImmutable",
                "No se puede renombrar un método de pago del sistema.");
        }

        _db.Entry(existing).CurrentValues.SetValues(paymentMethod);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Elimina un <see cref="PaymentMethod"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Valida RN-3: la entidad debe existir; si no, devuelve
    /// <c>Result.Failure("NotFound", ...)</c>. Valida RN-2: no permite
    /// eliminar un método de pago del sistema (<c>IsSystem = true</c>).
    /// </remarks>
    /// <param name="id">Identificador del método de pago a eliminar.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con éxito si se eliminó; de lo contrario,
    /// un error con código <c>NotFound</c> o <c>SystemPaymentMethodImmutable</c>.
    /// </returns>
    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(
                "NotFound",
                $"Método de pago {id} no encontrado.");
        }

        PaymentMethod? existing = await _db.PaymentMethods.FindAsync(new object?[] { id }, ct);
        if (existing is null)
        {
            return Result.Failure(
                "NotFound",
                $"Método de pago {id} no encontrado.");
        }

        if (existing.IsSystem)
        {
            return Result.Failure(
                "SystemPaymentMethodImmutable",
                "No se puede eliminar un método de pago del sistema.");
        }

        _db.PaymentMethods.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return Result.Success(null!);
    }

    /// <summary>
    /// Obtiene un <see cref="PaymentMethod"/> por su identificador.
    /// </summary>
    /// <remarks>
    /// Implementa RN-3: devuelve <see langword="null"/> si la entidad no existe.
    /// </remarks>
    /// <param name="id">Identificador del método de pago.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// El método de pago encontrado, o <see langword="null"/> si no existe.
    /// </returns>
    public async Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _db.PaymentMethods.FindAsync(new object?[] { id }, ct);
    }

    /// <summary>
    /// Obtiene todos los <see cref="PaymentMethod"/> ordenados alfabéticamente por nombre.
    /// </summary>
    /// <remarks>
    /// La consulta utiliza <c>AsNoTracking</c> porque es de solo lectura.
    /// </remarks>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Colección de solo lectura con todos los métodos de pago ordenados por <c>Name</c>.
    /// </returns>
    public async Task<IReadOnlyList<PaymentMethod>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.PaymentMethods
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }
}
