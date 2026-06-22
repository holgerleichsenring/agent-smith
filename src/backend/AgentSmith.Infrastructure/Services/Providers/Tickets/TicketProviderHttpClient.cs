using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgentSmith.Infrastructure.Services;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: thin wrapper around <see cref="HttpClient"/> for the REST-based
/// ticket providers (Jira, GitLab). Bundles the per-platform auth header
/// (Bearer / Basic / PRIVATE-TOKEN) and the canonical send-deserialise-or-throw
/// flow so the provider classes can stay focused on endpoint URLs +
/// IFieldMapper invocation.
/// <para>
/// This is composition, NOT inheritance: providers inject one of these and
/// call its methods. No <c>TicketProviderBase</c> exists; the shared shape
/// lives here.
/// </para>
/// </summary>
internal sealed class TicketProviderHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly Action<HttpRequestMessage> _applyAuth;

    private TicketProviderHttpClient(HttpClient httpClient, Action<HttpRequestMessage> applyAuth)
    {
        _httpClient = httpClient;
        _applyAuth = applyAuth;
    }

    /// <summary>
    /// Builds a client that adds a <c>PRIVATE-TOKEN</c> header (GitLab v4 REST).
    /// </summary>
    public static TicketProviderHttpClient WithPrivateToken(HttpClient httpClient, string token)
        => new(httpClient, req => req.Headers.Add("PRIVATE-TOKEN", token));

    /// <summary>
    /// Builds a client that adds <c>Authorization: Basic base64(email:token)</c>
    /// (Jira Cloud). Also sets <c>Accept: application/json</c> on the shared
    /// <see cref="HttpClient"/> defaults so plain GET calls receive JSON.
    /// </summary>
    public static TicketProviderHttpClient WithBasicAuth(
        HttpClient httpClient, string email, string apiToken)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        if (!httpClient.DefaultRequestHeaders.Accept.Any())
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        // Auth is on the client defaults, so per-request _applyAuth is a no-op.
        return new TicketProviderHttpClient(httpClient, _ => { });
    }

    /// <summary>
    /// Sends a request, returns the parsed <see cref="JsonDocument"/> on 2xx.
    /// On 404 returns <c>null</c> so callers can map to a not-found exception
    /// in their domain language. On other non-2xx throws (with body included).
    /// </summary>
    public async Task<JsonDocument?> SendForJsonAsync(
        HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, url, body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sends a request and returns the parsed <see cref="JsonDocument"/> on 2xx.
    /// On ANY non-2xx response (including 404) reads the response body and throws
    /// an <see cref="HttpRequestException"/> carrying the status code plus the
    /// provider's error message, so callers surface the real Jira/GitLab failure
    /// instead of silently degrading to an empty result. List-style callers wrap
    /// this in a try/catch to log the cause and fall back to an empty collection.
    /// </summary>
    public async Task<JsonDocument> SendForJsonOrThrowAsync(
        HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, url, body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        await response.EnsureSuccessWithBodyAsync(cancellationToken);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sends a request and throws on non-2xx (no response body returned).
    /// </summary>
    public async Task SendAsync(
        HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, url, body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await response.EnsureSuccessWithBodyAsync(cancellationToken);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        _applyAuth(request);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }
}
