using System.Net;
using System.Text;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;

namespace AgentSmith.Tests.Providers.Pipelines;

/// <summary>2026-11-18-3863e: a host's REST API answered from a table of "METHOD url-suffix" routes, recording every
/// request with its body.</summary>
internal sealed class FakePipelineHost : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new(StringComparer.Ordinal);

    public List<(string Method, string Url, string? Body)> Requests { get; } = [];

    public FakePipelineHost On(string method, string suffix, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[$"{method} {suffix}"] = () => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        return this;
    }

    public PipelineHttp Http() => new(new HttpClient(this), _ => { });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!.ToString();
        Requests.Add((request.Method.Method, url, request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
        var route = _routes.FirstOrDefault(r => r.Key.StartsWith(request.Method.Method + " ", StringComparison.Ordinal)
            && url.EndsWith(r.Key[(request.Method.Method.Length + 1)..], StringComparison.Ordinal));
        return route.Value?.Invoke() ?? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"message\":\"Not Found\"}") };
    }
}
