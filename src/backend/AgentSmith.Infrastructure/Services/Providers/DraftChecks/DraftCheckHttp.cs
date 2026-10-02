using System.Net.Http.Json;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: the draft check's HTTP client. Its named client has auto-redirect OFF — a
/// typed host answering 302 to elsewhere must not receive a PRIVATE-TOKEN header, which survives
/// a redirect. Each call has <see cref="StepTimeout"/>; the caller's token carries the overall budget.
/// </summary>
public sealed class DraftCheckHttp(IHttpClientFactory clients) : IDraftCheckHttp
{
    public const string ClientName = "draft-check";
    public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

    public Task<DraftCheckAnswer> GetAsync(string url, DraftCheckAuth auth, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, url, null, auth, cancellationToken);

    public Task<DraftCheckAnswer> PostJsonAsync(
        string url, object body, DraftCheckAuth auth, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, url, JsonContent.Create(body), auth, cancellationToken);

    private async Task<DraftCheckAnswer> SendAsync(
        HttpMethod method, string url, HttpContent? content, DraftCheckAuth auth, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            return DraftCheckAnswer.Unanswered(url, "The host is not an http(s) address.");
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        var host = uri.Host;
        auth.Apply(request);
        using var step = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        step.CancelAfter(StepTimeout);
        try
        {
            using var response = await clients.CreateClient(ClientName).SendAsync(request, step.Token);
            var body = await response.Content.ReadAsStringAsync(step.Token);
            return DraftCheckAnswer.Answered(host, (int)response.StatusCode, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DraftCheckAnswer.Unanswered(host, $"{host} gave no answer within {StepTimeout.TotalSeconds:0} s.");
        }
        catch (HttpRequestException ex)
        {
            return DraftCheckAnswer.Unanswered(host, Reason(host, ex.HttpRequestError));
        }
    }

    private static string Reason(string host, HttpRequestError error) => error switch
    {
        HttpRequestError.NameResolutionError => $"The host name {host} does not resolve (DNS).",
        HttpRequestError.ConnectionError => $"No connection to {host} could be opened.",
        HttpRequestError.SecureConnectionError => $"The TLS handshake with {host} failed.",
        _ => $"{host} could not be reached ({error}).",
    };
}
