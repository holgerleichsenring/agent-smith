using System.Net;
using System.Net.Http.Headers;
using AgentSmith.Infrastructure.Services.Providers.DraftChecks;

namespace AgentSmith.Tests.ConfigStudio.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: a host that answers by URL prefix (longest wins), records every request with
/// its auth headers, and answers 404 for anything unscripted.
/// </summary>
internal sealed class ScriptedHostHandler : HttpMessageHandler
{
    private readonly List<(string Prefix, Func<HttpResponseMessage> Answer)> _routes = [];

    public List<(string Url, string Auth, string Body)> Requests { get; } = [];

    public ScriptedHostHandler On(string prefix, int status, string body, string? location = null)
    {
        _routes.Add((prefix, () =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
            if (location is not null) response.Headers.Location = new Uri(location);
            return response;
        }));
        return this;
    }

    public ScriptedHostHandler Throws(string prefix, HttpRequestError error)
    {
        _routes.Add((prefix, () => throw new HttpRequestException(error, "socket says no")));
        return this;
    }

    public IDraftCheckHttp Http() => new DraftCheckHttp(new Factory(this));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((url, AuthOf(request.Headers), body));
        var route = _routes.Where(r => url.StartsWith(r.Prefix, StringComparison.Ordinal))
            .OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
        return route.Answer is null ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }
            : route.Answer();
    }

    private static string AuthOf(HttpRequestHeaders headers) =>
        headers.TryGetValues("PRIVATE-TOKEN", out var token) ? token.Single()
        : headers.Authorization?.ToString() ?? string.Empty;

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
