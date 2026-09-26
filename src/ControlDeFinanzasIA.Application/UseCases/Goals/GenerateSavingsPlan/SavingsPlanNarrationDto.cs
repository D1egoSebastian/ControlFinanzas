namespace ControlDeFinanzasIA.Application.UseCases.Goals.GenerateSavingsPlan;

/// <summary>
/// Estado de la narracion generada por IA para el plan de ahorro de una meta
/// (F-016, R-4, D-1). Nunca representa un fallo: el use case es
/// success-shaped para toda causa relacionada con IA — solo "meta no
/// encontrada" produce <c>Result.Failure</c>.
/// </summary>
public enum SavingsPlanNarrationStatus
{
    /// <summary>La IA genero una narracion que paso el guard numerico.</summary>
    Generated = 0,

    /// <summary>No hay proveedor de IA configurado; no se hizo ninguna llamada.</summary>
    NotConfigured = 1,

    /// <summary>
    /// La IA esta configurada pero la narracion no esta disponible (fallo,
    /// timeout, salida vacia o rechazada por el guard numerico).
    /// </summary>
    Unavailable = 2,
}

/// <summary>
/// Resultado de <see cref="GenerateSavingsPlanUseCase.ExecuteAsync"/> (F-016,
/// R-4, D-1). La figura determinista de la fila nunca depende de este DTO:
/// <c>Text</c> solo aporta la explicacion, nunca el numero.
/// </summary>
/// <param name="Status">Estado de la narracion.</param>
/// <param name="Text">
/// Texto de 2 a 4 oraciones ya limpiado y verificado por
/// <see cref="SavingsPlanNarrationGuard"/>. <c>null</c> salvo cuando
/// <paramref name="Status"/> es <see cref="SavingsPlanNarrationStatus.Generated"/>.
/// </param>
public sealed record SavingsPlanNarrationDto(SavingsPlanNarrationStatus Status, string? Text);
