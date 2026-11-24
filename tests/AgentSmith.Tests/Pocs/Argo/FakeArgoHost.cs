using System.Net;
using System.Text;

namespace AgentSmith.Tests.Pocs.Argo;

/// <summary>2026-11-23-c81ec: Argo CD's API over HTTP — answers recorded JSON per path (404 for any other), and records
/// every request with its path and query, Authorization header and body.</summary>
internal sealed class FakeArgoHost : HttpMessageHandler
{
    public Dictionary<string, (HttpStatusCode Status, string Body)> Answers { get; } = [];

    public List<(string Method, Uri Url, string Auth, string? Body)> Requests { get; } = [];

    public HttpClient Client() => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method.Method, request.RequestUri!, request.Headers.Authorization?.ToString() ?? string.Empty, body));
        return Answers.TryGetValue(request.RequestUri!.AbsolutePath, out var answer)
            ? new HttpResponseMessage(answer.Status) { Content = new StringContent(answer.Body, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":"not found"}""") };
    }

    /// <summary>A JWT with <paramref name="exp"/> (Unix seconds) or none, signed with nothing — Argo verifies, not us.</summary>
    public static string Jwt(long? exp)
    {
        static string Part(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = exp is { } e ? $$"""{"iss":"argocd","sub":"proj:pocs:agent-smith","exp":{{e}}}""" : """{"iss":"argocd","sub":"proj:pocs:agent-smith"}""";
        return $"{Part("""{"alg":"HS256","typ":"JWT"}""")}.{Part(payload)}.c2lnbmF0dXJl";
    }
}
