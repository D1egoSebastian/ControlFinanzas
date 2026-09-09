using ControlDeFinanzasIA.Domain.Catalog;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Data transfer object que representa un <see cref="PaymentMethod"/> en
/// la capa de aplicacion. Pensado para alimentar el picker de la UI y
/// cualquier consulta donde solo se necesita identidad + nombre
/// legible + flag de sistema.
/// </summary>
/// <param name="Id">Identificador unico del metodo.</param>
/// <param name="Name">Nombre legible (ej. <c>"Efectivo"</c>).</param>
/// <param name="IsSystem">
/// <c>true</c> si el metodo es del sistema (no se puede borrar desde
/// la UI; ver <c>PaymentMethodRepository.RemoveAsync</c>). La UI lo
/// usa para mostrar el icono de candado o deshabilitar el menu.
/// </param>
public sealed record PaymentMethodDto(Guid Id, string Name, bool IsSystem);
