using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs;
using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;

/// <summary>
/// Query de lectura que devuelve todos los metodos de pago
/// persistidos, ordenados por nombre, mapeados a
/// <see cref="PaymentMethodDto"/>.
/// </summary>
/// <remarks>
/// No hay paginacion: el catalogo de metodos de pago es pequeno
/// (5 del sistema + N personalizados esperados), asi que el caller
/// recibe la lista completa. Si en el futuro se vuelve grande, se
/// anade paginacion en una nueva feature.
/// <para>
/// El metodo retorna <see cref="Result"/> non-generico. En exito,
/// <see cref="Result.Value"/> es <c>IReadOnlyList&lt;PaymentMethodDto&gt;</c>.
/// </para>
/// </remarks>
public sealed class GetPaymentMethodsQuery
{
    private readonly IPaymentMethodRepository _repository;
    private readonly ILogger<GetPaymentMethodsQuery> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPaymentMethodsQuery"/> class.
    /// </summary>
    /// <param name="repository">Repositorio de metodos de pago.</param>
    /// <param name="logger">Logger para diagnosticar errores del repositorio.</param>
    public GetPaymentMethodsQuery(
        IPaymentMethodRepository repository,
        ILogger<GetPaymentMethodsQuery> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el query de catalogo de metodos de pago.
    /// </summary>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con <see cref="IReadOnlyList{PaymentMethodDto}"/>
    /// en <see cref="Result.Value"/> (ordenados por nombre) si la operacion
    /// tiene exito; de lo contrario, un <see cref="Error"/> con codigo
    /// <c>"RepositoryError"</c>.
    /// </returns>
    public async Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<PaymentMethod> methods = await _repository.GetAllAsync(ct);
            IReadOnlyList<PaymentMethodDto> dtos = methods
                .Select(m => m.ToDto())
                .ToList();
            return Result.Success(dtos);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el catalogo de metodos de pago.");
            return Result.Failure("RepositoryError", "Ocurrio un error al obtener los metodos de pago.");
        }
    }
}
