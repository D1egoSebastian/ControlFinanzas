using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ILLMClientFactory"/> used in unit tests
/// (F-013, slice 5b). Records every <see cref="AppSettings"/> passed to
/// <see cref="Resolve"/>/<see cref="ResolveDiagnostic"/> so a test can
/// assert the use case never calls the factory when a guard should
/// short-circuit.
/// </summary>
public sealed class FakeLlmClientFactory : ILLMClientFactory
{
    /// <summary>Gets or sets the resolution returned by <see cref="Resolve"/>. Defaults to "not configured".</summary>
    public LlmClientResolution ResolveResult { get; set; } =
        LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey);

    /// <summary>Gets or sets the resolution returned by <see cref="ResolveDiagnostic"/>. Defaults to "not configured".</summary>
    public LlmClientResolution ResolveDiagnosticResult { get; set; } =
        LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey);

    /// <summary>Every <see cref="AppSettings"/> passed to <see cref="Resolve"/>, in call order.</summary>
    public List<AppSettings> ResolveCalls { get; } = [];

    /// <summary>Every <see cref="AppSettings"/> passed to <see cref="ResolveDiagnostic"/>, in call order.</summary>
    public List<AppSettings> ResolveDiagnosticCalls { get; } = [];

    /// <summary>
    /// When set, <see cref="ResolveDiagnostic"/> throws this exception
    /// instead of returning <see cref="ResolveDiagnosticResult"/> (F-013,
    /// slice 7). Drives the defensive catch-all path in
    /// <c>TestAiConnectionUseCase</c> without a real transport failure.
    /// </summary>
    public Exception? ThrowOnResolveDiagnostic { get; set; }

    /// <inheritdoc />
    public LlmClientResolution Resolve(AppSettings settings)
    {
        ResolveCalls.Add(settings);
        return ResolveResult;
    }

    /// <inheritdoc />
    public LlmClientResolution ResolveDiagnostic(AppSettings settings)
    {
        ResolveDiagnosticCalls.Add(settings);

        if (ThrowOnResolveDiagnostic is not null)
        {
            throw ThrowOnResolveDiagnostic;
        }

        return ResolveDiagnosticResult;
    }
}
