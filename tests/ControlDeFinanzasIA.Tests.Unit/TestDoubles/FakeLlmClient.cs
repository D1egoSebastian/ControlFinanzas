using ControlDeFinanzasIA.Application.Abstractions.Llm;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ILLMClient"/> used in unit tests
/// (F-013, slice 5b). Records every <see cref="CompleteAsync"/> request so
/// a test can assert the exact <see cref="LlmRequest"/> sent (temperature,
/// max tokens, correlation id, prompt content) without a mocking library.
/// </summary>
public sealed class FakeLlmClient : ILLMClient
{
    /// <inheritdoc />
    public string ProviderName { get; set; } = "Fake";

    /// <summary>Gets or sets the response returned by <see cref="CompleteAsync"/> when <see cref="ExceptionToThrow"/> is <c>null</c>.</summary>
    public LlmResponse ResponseToReturn { get; set; } = new(string.Empty, 0, 0);

    /// <summary>
    /// When set, <see cref="CompleteAsync"/> throws this exception instead
    /// of returning <see cref="ResponseToReturn"/>.
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>Gets or sets the outcome returned by <see cref="TestConnectionAsync"/>.</summary>
    public LlmConnectionOutcome ConnectionOutcome { get; set; } = new(LlmConnectionStatus.Ok, "Fake");

    /// <summary>Every <see cref="LlmRequest"/> passed to <see cref="CompleteAsync"/>, in call order.</summary>
    public List<LlmRequest> CompleteRequests { get; } = [];

    /// <summary>Number of times <see cref="CompleteAsync"/> was called. Zero means the use case never called the client.</summary>
    public int CompleteCallCount => CompleteRequests.Count;

    /// <inheritdoc />
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        CompleteRequests.Add(request);

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        ct.ThrowIfCancellationRequested();

        return Task.FromResult(ResponseToReturn);
    }

    /// <inheritdoc />
    public Task<LlmConnectionOutcome> TestConnectionAsync(CancellationToken ct = default)
        => Task.FromResult(ConnectionOutcome);
}
