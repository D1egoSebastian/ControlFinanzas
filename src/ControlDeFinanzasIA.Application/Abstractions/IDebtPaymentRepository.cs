using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar entidades <see cref="DebtPayment"/>.
/// </summary>
public interface IDebtPaymentRepository
{
    /// <summary>
    /// A&#241;ade una nueva <see cref="DebtPayment"/> al store.
    /// </summary>
    /// <param name="payment">Entidad a a&#241;adir.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operaci&#243;n tuvo &#233;xito,
    /// o con <c>Error</c> si fall&#243; (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(DebtPayment payment, CancellationToken ct = default);

    /// <summary>
    /// Actualiza una <see cref="DebtPayment"/> existente.
    /// </summary>
    /// <param name="payment">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operaci&#243;n tuvo &#233;xito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(DebtPayment payment, CancellationToken ct = default);

    /// <summary>
    /// Elimina una <see cref="DebtPayment"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador de la entidad a eliminar.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operaci&#243;n tuvo &#233;xito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene una <see cref="DebtPayment"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador de la entidad.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    Task<DebtPayment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las <see cref="DebtPayment"/>. La colecci&#243;n
    /// retornada es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    Task<IReadOnlyList<DebtPayment>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene el calendario completo de cuotas de una deuda, ordenado
    /// por <see cref="DebtPayment.InstallmentNumber"/> ascendente.
    /// </summary>
    /// <param name="debtId">Identificador de la <c>Debt</c> due&#241;a.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    Task<IReadOnlyList<DebtPayment>> GetByDebtIdAsync(Guid debtId, CancellationToken ct = default);
}
