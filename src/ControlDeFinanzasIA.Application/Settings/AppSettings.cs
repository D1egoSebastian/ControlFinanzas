namespace ControlDeFinanzasIA.Application.Settings;

/// <summary>
/// Preferencia de tema visual. Ambos valores se aplican de verdad
/// (modo oscuro, pre-1.0): la UI los traduce a una variante de tema de
/// Avalonia al arrancar y en vivo al cambiar el selector de Ajustes. Los
/// valores persistidos por versiones anteriores (que guardaban la
/// preferencia sin aplicarla) siguen siendo validos: el nombre del
/// miembro es lo que se guarda.
/// </summary>
public enum AppTheme
{
    Dark = 0,
    Light = 1,
}

/// <summary>
/// Proveedor de IA configurado para las llamadas que hará la Fase 13.
/// Fase 12 solo persiste este valor; no existe ningún adaptador ni
/// llamada de red en este codebase todavía.
/// </summary>
public enum AiProvider
{
    OpenAI = 0,
    Anthropic = 1,
    OpenRouter = 2,
    Custom = 3,
}

/// <summary>
/// Modelo de lectura completo de las preferencias del usuario,
/// compuesto por <c>ISettingsStore.GetAppSettingsAsync</c> a partir de
/// las filas <c>Setting</c>. La lista de campos es fija verbatim por
/// <c>docs/architecture.md</c> §11; la Fase 13's <c>ILLMClientFactory</c>
/// consume exactamente estos campos.
/// </summary>
public sealed record AppSettings(
    AppTheme Theme,
    string Locale,
    AiProvider AiProvider,
    string AiApiKey,
    string AiModel,
    string? AiBaseUrl,
    double AiTemperature,
    int AiMaxTokensPerRequest)
{
    /// <summary>
    /// Tema por defecto de primer arranque. Es <see cref="AppTheme.Light"/>: es el unico aspecto que
    /// tenian las versiones anteriores, asi que una instalacion sin preferencia guardada no cambia de
    /// aspecto al aparecer el modo oscuro.
    /// </summary>
    public const AppTheme DefaultTheme = AppTheme.Light;

    /// <summary>Proveedor de IA por defecto de primer arranque.</summary>
    public const AiProvider DefaultAiProvider = AiProvider.OpenAI;

    /// <summary>Temperatura por defecto de primer arranque.</summary>
    public const double DefaultAiTemperature = 0.7d;

    /// <summary>Maximo de tokens por request por defecto de primer arranque.</summary>
    public const int DefaultAiMaxTokensPerRequest = 2_000;

    /// <summary>Cota inferior inclusiva valida para <see cref="AiTemperature"/>.</summary>
    public const double MinAiTemperature = 0.0d;

    /// <summary>Cota superior inclusiva valida para <see cref="AiTemperature"/>.</summary>
    public const double MaxAiTemperature = 2.0d;

    /// <summary>Cota inferior inclusiva valida para <see cref="AiMaxTokensPerRequest"/>.</summary>
    public const int MinAiMaxTokensPerRequest = 1;

    /// <summary>Cota superior inclusiva valida para <see cref="AiMaxTokensPerRequest"/>.</summary>
    public const int MaxAiMaxTokensPerRequest = 128_000;

    /// <summary>
    /// Valores de primer arranque. La tabla <c>Setting</c> vacia o
    /// ilegible produce exactamente esto — nunca <see langword="null"/>,
    /// nunca una excepción.
    /// </summary>
    public static AppSettings Defaults { get; } = new(
        DefaultTheme,
        SupportedLocales.Default,
        DefaultAiProvider,
        string.Empty,
        string.Empty,
        null,
        DefaultAiTemperature,
        DefaultAiMaxTokensPerRequest);
}
