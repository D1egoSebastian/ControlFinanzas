using System.Net;
using System.Text;

namespace ControlDeFinanzasIA.Tests.Integration.TestDoubles;

/// <summary>
/// Hand-rolled <see cref="HttpMessageHandler"/> double for the Fase 13 LLM
/// adapter tests (D-14). No mocking library is used anywhere in this
/// solution; this matches the <c>Fake*Repository</c> convention.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    private readonly List<CapturedRequest> _requests = new();

    /// <summary>Canned-response constructor: every request gets the same status and body.</summary>
    /// <param name="status">Status code to return.</param>
    /// <param name="body">Response body, sent as <c>application/json</c>.</param>
    public FakeHttpMessageHandler(HttpStatusCode status, string body)
        : this((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }))
    {
    }

    /// <summary>Responder-delegate constructor: full control over the response per request.</summary>
    /// <param name="responder">Builds the response for each captured request.</param>
    public FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        ArgumentNullException.ThrowIfNull(responder);
        _responder = responder;
    }

    /// <summary>Every request seen, in order, with the body already buffered to a string.</summary>
    public IReadOnlyList<CapturedRequest> Requests => _requests;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // HttpRequestMessage.Content is disposed by the pipeline by the
        // time an assertion would run, so the body is buffered here, not
        // held as a live reference (D-14).
        string body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        if (request.Content is not null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }
        }

        _requests.Add(new CapturedRequest(request.Method, request.RequestUri, headers, body));

        return await _responder(request, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>A single request captured by <see cref="FakeHttpMessageHandler"/>.</summary>
/// <param name="Method">HTTP method used.</param>
/// <param name="Uri">Absolute request URI.</param>
/// <param name="Headers">Request and content headers, combined, keyed case-insensitively.</param>
/// <param name="Body">Request body, already buffered to a string.</param>
public sealed record CapturedRequest(
    HttpMethod Method,
    Uri? Uri,
    IReadOnlyDictionary<string, string> Headers,
    string Body);
