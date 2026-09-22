using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Infrastructure.Llm.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace ControlDeFinanzasIA.Infrastructure.Llm;

/// <summary>
/// Extension methods para registrar el pipeline de IA (F-013, slice 4, D-9)
/// en el contenedor de DI: 8 <see cref="HttpClient"/>s nombrados (uno
/// principal y uno de diagnóstico por cada <see cref="AiProvider"/>),
/// <see cref="ILLMClientFactory"/> e <see cref="IPromptProvider"/>. Firma
/// deliberadamente sin ningún tipo de <c>Microsoft.Extensions.Http</c> ni
/// <c>System.Net.Http</c> — <c>Bootstrap</c> (UI) solo puede nombrar
/// <see cref="IServiceCollection"/>, para no violar R-4.
/// </summary>
public static class LlmServiceCollectionExtensions
{
    /// <summary>Presupuesto total de la solicitud, retries incluidos (R-9, D-9).</summary>
    private static readonly TimeSpan _totalRequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Timeout por intento individual (R-9, D-9).</summary>
    private static readonly TimeSpan _attemptTimeout = TimeSpan.FromSeconds(6);

    /// <summary>Delay base del backoff exponencial jitterizado (R-9, D-9).</summary>
    private static readonly TimeSpan _retryBaseDelay = TimeSpan.FromSeconds(1);

    /// <summary>Ventana de muestreo del circuit breaker (R-9, D-9).</summary>
    private static readonly TimeSpan _circuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);

    /// <summary>Duración del breaker abierto antes de volver a probar (R-9, D-9).</summary>
    private static readonly TimeSpan _circuitBreakerBreakDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Timeout del pipeline de diagnóstico ("Probar conexión") — sin retry,
    /// sin breaker (R-8, R-9, D-9): un diagnóstico debe reportar el primer
    /// resultado honesto, nunca disfrazarlo con un reintento, y nunca debe
    /// bloquearse por un breaker abierto del cliente principal (nombre
    /// distinto, estado de handler separado).
    /// </summary>
    private static readonly TimeSpan _diagnosticTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Registra los 8 <see cref="HttpClient"/>s nombrados del pipeline de
    /// IA (<c>llm-{provider}</c> y <c>llm-{provider}-diagnostic</c> por
    /// cada uno de los 4 <see cref="AiProvider"/>), <see cref="ILLMClientFactory"/>
    /// (Singleton) e <see cref="IPromptProvider"/> (Singleton). No fija
    /// <c>BaseAddress</c> en ningún cliente: la base URL resuelta (D-3)
    /// llega recién al momento de <see cref="ILLMClientFactory.Resolve"/>,
    /// porque puede cambiar entre dos resoluciones sin reinicio de la app.
    /// </summary>
    /// <param name="services">Colección de servicios de DI.</param>
    /// <returns>La misma <see cref="IServiceCollection"/> para encadenar.</returns>
    /// <exception cref="ArgumentNullException">
    /// Se lanza si <paramref name="services"/> es <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddLlmClients(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (AiProvider provider in Enum.GetValues<AiProvider>())
        {
            services
                .AddHttpClient($"llm-{provider}")
                .AddStandardResilienceHandler(ConfigureMainPipeline);

            services
                .AddHttpClient($"llm-{provider}-diagnostic")
                .AddResilienceHandler(
                    $"llm-{provider}-diagnostic-pipeline",
                    builder => builder.AddTimeout(_diagnosticTimeout));
        }

        services.AddSingleton<ILLMClientFactory, LLMClientFactory>();
        services.AddSingleton<IPromptProvider, EmbeddedPromptProvider>();

        return services;
    }

    private static void ConfigureMainPipeline(HttpStandardResilienceOptions options)
    {
        options.TotalRequestTimeout.Timeout = _totalRequestTimeout;
        options.AttemptTimeout.Timeout = _attemptTimeout;

        options.Retry.MaxRetryAttempts = 1;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.Delay = _retryBaseDelay;

        // Retry predicate: handler default (does not retry 400/401/403) —
        // the single most valuable default to keep (D-9). A wrong key
        // fails in ~1s instead of burning 15s and two billed calls.

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 4;
        options.CircuitBreaker.SamplingDuration = _circuitBreakerSamplingDuration;
        options.CircuitBreaker.BreakDuration = _circuitBreakerBreakDuration;

        // Rate limiter: handler default, untouched (D-9) — already
        // redundant given D-12's in-flight guard, which caps concurrency
        // at 1 on the caller side.
    }
}
