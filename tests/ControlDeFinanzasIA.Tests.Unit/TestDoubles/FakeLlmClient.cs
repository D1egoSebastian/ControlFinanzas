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

    /// <summary>
    /// When set, <see cref="CompleteAsync"/> delegates to this callback
    /// instead of <see cref="ResponseToReturn"/>/<see cref="ExceptionToThrow"/>
    /// (F-014, slice 6, D-13). Lets a test build a never-completing client
    /// (via a <see cref="TaskCompletionSource{TResult}"/>, for spec S-9) or
    /// route a response by inspecting the request content, so one fake can
    /// answer the insights call and the anomaly-narration batch call
    /// differently.
    /// </summary>
    public Func<LlmRequest, CancellationToken, Task<LlmResponse>>? OnCompleteAsync { get; set; }

    /// <summary>
    /// Number of times <see cref="TestConnectionAsync"/> was called (F-013,
    /// slice 7). Zero means the use case never made a diagnostic round
    /// trip — the assertion the "not configured" outcome relies on.
    /// </summary>
    public int TestConnectionCallCount { get; private set; }

    /// <summary>
    /// When set, <see cref="TestConnectionAsync"/> awaits this
    /// <see cref="TaskCompletionSource{TResult}"/> before returning
    /// <see cref="ConnectionOutcome"/> (F-013, slice 7). Lets a test hold
    /// the round trip open to assert an in-flight busy state without a
    /// real network call or a sleep.
    /// </summary>
    public TaskCompletionSource<bool>? TestConnectionGate { get; set; }

    /// <inheritdoc />
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        CompleteRequests.Add(request);

        if (OnCompleteAsync is not null)
        {
            return OnCompleteAsync(request, ct);
        }

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        ct.ThrowIfCancellationRequested();

        return Task.FromResult(ResponseToReturn);
    }

    /// <inheritdoc />
    public async Task<LlmConnectionOutcome> TestConnectionAsync(CancellationToken ct = default)
    {
        TestConnectionCallCount++;

        if (TestConnectionGate is not null)
        {
            await TestConnectionGate.Task.ConfigureAwait(false);
        }

        return ConnectionOutcome;
    }
}
