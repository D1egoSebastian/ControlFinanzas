using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.UseCases.Settings.TestAiConnection;

/// <summary>
/// Diagnostica la configuración de IA persistida contra el botón "Probar
/// conexión" de Settings (F-013, slice 7, R-8, D-11). Usa
/// <see cref="ILLMClientFactory.ResolveDiagnostic"/> — el pipeline sin
/// retry y sin circuit breaker (D-9) — para que una key recién corregida
/// nunca choque con un breaker abierto por la anterior (S-13).
/// </summary>
/// <remarks>
/// El constructor NO recibe <c>HttpClient</c> ni <c>IHttpClientFactory</c>,
/// la misma garantía de aislamiento HTTP de <c>CategorizeExpenseUseCase</c>.
/// Las cuatro variantes de <see cref="AiConnectionResultKind"/> viajan
/// siempre dentro de un <c>Result.Success</c>: diagnosticar ES el trabajo de
/// este use case y lo completa en los cuatro casos. <c>Result.Failure</c>
/// queda reservado para que el propio use case falle inesperadamente — algo
/// que, dado que <see cref="ISettingsStore.GetAppSettingsAsync"/> y
/// <see cref="ILLMClient.TestConnectionAsync"/> nunca lanzan, debería ser
/// inalcanzable, y el <c>catch</c> amplio existe solo como defensa en
/// profundidad.
/// </remarks>
public sealed class TestAiConnectionUseCase
{
    /// <summary>
    /// Techo de defensa en profundidad para el round trip de diagnóstico,
    /// igual al de <c>CategorizeExpenseUseCase</c> (R-9).
    /// </summary>
    public static readonly TimeSpan HardTimeout = TimeSpan.FromSeconds(15);

    private readonly ISettingsStore _settingsStore;
    private readonly ILLMClientFactory _factory;
    private readonly ILogger<TestAiConnectionUseCase> _logger;

    /// <summary>Inicializa una nueva instancia de <see cref="TestAiConnectionUseCase"/>.</summary>
    /// <param name="settingsStore">Fuente de <see cref="AppSettings"/> — nunca lanza, nunca devuelve null.</param>
    /// <param name="factory">Resuelve el cliente de diagnóstico configurado, o "no configurado".</param>
    /// <param name="logger">Logger para diagnóstico y el Warning defensivo con correlation id.</param>
    public TestAiConnectionUseCase(
        ISettingsStore settingsStore,
        ILLMClientFactory factory,
        ILogger<TestAiConnectionUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el diagnóstico de conexión contra la configuración de IA
    /// GUARDADA (persistida vía <see cref="ISettingsStore"/>), no contra
    /// valores sin guardar de un formulario en memoria.
    /// </summary>
    /// <param name="ct">Token de cancelación del caller.</param>
    /// <returns>
    /// Siempre <see cref="Result.Success(object)"/> con un
    /// <see cref="TestAiConnectionDto"/> cuyo <see cref="AiConnectionResultKind"/>
    /// distingue las cuatro variantes (R-8).
    /// </returns>
    public async Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();
        AppSettings settings = await _settingsStore.GetAppSettingsAsync(ct).ConfigureAwait(false);

        try
        {
            LlmClientResolution resolution = _factory.ResolveDiagnostic(settings);
            if (resolution.Status != LlmConfigurationStatus.Configured || resolution.Client is null)
            {
                _logger.LogDebug(
                    "AI connection test skipped: not configured. CorrelationId={CorrelationId} Status={Status}",
                    correlationId,
                    resolution.Status);
                return Result.Success(new TestAiConnectionDto(AiConnectionResultKind.NotConfigured, settings.AiProvider.ToString()));
            }

            ILLMClient client = resolution.Client;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(HardTimeout);

            LlmConnectionOutcome outcome = await client.TestConnectionAsync(linked.Token).ConfigureAwait(false);

            AiConnectionResultKind kind = outcome.Status switch
            {
                LlmConnectionStatus.Ok => AiConnectionResultKind.Ok,
                LlmConnectionStatus.InvalidCredentials => AiConnectionResultKind.InvalidCredentials,
                LlmConnectionStatus.Unavailable => AiConnectionResultKind.Unavailable,
                _ => AiConnectionResultKind.Unavailable,
            };

            _logger.LogDebug(
                "AI connection test completed. CorrelationId={CorrelationId} Provider={Provider} Kind={Kind}",
                correlationId,
                outcome.ProviderName,
                kind);

            return Result.Success(new TestAiConnectionDto(kind, outcome.ProviderName));
        }
        catch (Exception ex)
        {
            // Amplio a propósito, defensa en profundidad: ni GetAppSettingsAsync
            // ni TestConnectionAsync deberían lanzar nunca (D-2, D-11), por lo
            // que este catch debería ser inalcanzable en producción. A
            // diferencia de las cuatro variantes de arriba, ESTO sí es el use
            // case fallando, no un diagnóstico completado — por eso es
            // Result.Failure, no un quinto AiConnectionResultKind.
            _logger.LogWarning(
                ex,
                "AI connection test failed unexpectedly. CorrelationId={CorrelationId}",
                correlationId);
            return Result.Failure("AiTestConnectionUnexpectedError", "No se pudo completar la prueba de conexión.");
        }
    }
}
