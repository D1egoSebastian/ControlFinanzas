namespace ControlDeFinanzasIA.Domain.Enums;

/// <summary>
/// Monedas soportadas por la aplicación. Un <c>Money</c> lleva siempre
/// una de estas monedas.
/// Inicialmente DOP y USD; añadir un valor (por ejemplo EUR) es un
/// cambio aislado que no rompe la API pública de <c>Money</c>
/// siempre que no se reusen los ordinales existentes.
/// </summary>
public enum Currency
{
    /// <summary>Peso dominicano (DOP).</summary>
    DOP = 0,

    /// <summary>Dólar estadounidense (USD).</summary>
    USD = 1
}
