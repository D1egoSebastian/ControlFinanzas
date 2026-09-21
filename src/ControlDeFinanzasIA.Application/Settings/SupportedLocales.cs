namespace ControlDeFinanzasIA.Application.Settings;

/// <summary>
/// Lista blanca de locales soportados por la aplicacion. No es
/// decoracion: <c>App.axaml.cs</c> alimenta el locale persistido
/// directamente a <c>new CultureInfo(locale)</c>, que lanza
/// <c>CultureNotFoundException</c> con basura. Sin esta lista blanca,
/// una escritura invalida en la fila <c>locale</c> tumbaria el arranque
/// de la app (F-012 D-4).
/// </summary>
public static class SupportedLocales
{
    /// <summary>Español (República Dominicana).</summary>
    public const string SpanishDominican = "es-DO";

    /// <summary>Inglés.</summary>
    public const string English = "en";

    /// <summary>Default de producto: "ES por defecto" (docs/vision.md).</summary>
    public const string Default = SpanishDominican;

    /// <summary>Lista completa de locales soportados, para el picker de la UI.</summary>
    public static IReadOnlyList<string> All { get; } = [SpanishDominican, English];

    /// <summary>
    /// Indica si <paramref name="locale"/> es uno de los locales
    /// soportados. <see langword="null"/> nunca es soportado.
    /// </summary>
    public static bool IsSupported(string? locale) =>
        locale is not null && All.Contains(locale, StringComparer.Ordinal);
}
