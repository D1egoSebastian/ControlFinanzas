using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Debts;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="Debt"/>.
/// </summary>
public interface IDebtRepository
{
    /// <summary>
    /// A&#241;ade una nueva <see cref="Debt"/> al store.
    /// </summary>
    /// <param name="debt">Entidad a a&#241;adir.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operaci&#243;n tuvo &#233;xito,
    /// o con <c>Error</c> si fall&#243; (p. ej. la entidad ya existe).
    /// </returns>
    Task<Result> AddAsync(Debt debt, CancellationToken ct = default);

    /// <summary>
    /// A&#241;ade una nueva <see cref="Debt"/> junto con el calendario
    /// completo de <see cref="DebtPayment"/> generado para ella, en una
    /// &#250;nica operaci&#243;n transaccional (R-3): si cualquiera de las
    /// escrituras falla, no se persiste ni la deuda ni sus cuotas.
    /// </summary>
    /// <param name="debt">Deuda a a&#241;adir.</param>
    /// <param name="payments">Calendario completo de cuotas de la deuda.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operaci&#243;n tuvo &#233;xito,
    /// o con <c>Error</c> si fall&#243;.
    /// </returns>
    Task<Result> AddWithScheduleAsync(Debt debt, IReadOnlyList<DebtPayment> payments, CancellationToken ct = default);

    /// <summary>
    /// Actualiza una <see cref="Debt"/> existente.
    /// </summary>
    /// <param name="debt">Entidad a actualizar.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operaci&#243;n tuvo &#233;xito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> UpdateAsync(Debt debt, CancellationToken ct = default);

    /// <summary>
    /// Elimina una <see cref="Debt"/> por su ID.
    /// </summary>
    /// <param name="id">Identificador del agregado a eliminar.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operaci&#243;n tuvo &#233;xito,
    /// o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene una <see cref="Debt"/> por su ID, o <c>null</c> si no existe.
    /// </summary>
    /// <param name="id">Identificador del agregado.</param>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    Task<Debt?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las <see cref="Debt"/>. La colecci&#243;n retornada
    /// es de solo lectura.
    /// </summary>
    /// <param name="ct">Token de cancelaci&#243;n opcional.</param>
    Task<IReadOnlyList<Debt>> GetAllAsync(CancellationToken ct = default);
}
