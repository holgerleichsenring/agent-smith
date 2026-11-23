using System.Net;
using System.Text;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: a GitLab API host faked at the HTTP layer. Routes match method and a path-and-query fragment; the
/// first registered match answers; anything unrouted answers 404. Every request is recorded with its body.
/// </summary>
internal sealed class GitLabHostFake : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string Part, Func<HttpResponseMessage> Answer)> _routes = [];

    public List<(HttpMethod Method, string PathAndQuery, string? Body)> Requests { get; } = [];

    public GitLabHostFake On(HttpMethod method, string part, HttpStatusCode status, string body = "{}",
        string? nextPage = null)
    {
        _routes.Add((method, part, () =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (nextPage is not null) response.Headers.Add("X-Next-Page", nextPage);
            return response;
        }));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var pathAndQuery = Uri.UnescapeDataString(request.RequestUri!.PathAndQuery);
        Requests.Add((request.Method, pathAndQuery,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
        var route = _routes.FirstOrDefault(r => r.Method == request.Method
            && pathAndQuery.Contains(r.Part, StringComparison.Ordinal));
        return route.Answer?.Invoke() ?? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
    }
}
