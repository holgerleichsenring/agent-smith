using System.Net;
using System.Text;
using System.Text.Json;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-25-9dbbd / 684b: GitHub GraphQL scripted under Octokit's REAL connection — a stubbed
/// IApiResponse hid that Octokit leaves a string Body empty for a JSON reply. Each POST is answered
/// by the reply function from the posted query and variables, and recorded.
/// </summary>
internal sealed class GitHubGraphQlHost
{
    public List<(Uri Url, string Query, JsonElement Variables)> Requests { get; } = [];

    public static IGitHubClient Replying(string reply) => new GitHubGraphQlHost().Client((_, _) => reply);

    public IGitHubClient Client(Func<string, JsonElement, string> reply) =>
        new GitHubClient(new Connection(new ProductHeaderValue("test"), GitHubClient.GitHubApiUrl,
            new InMemoryCredentialStore(new Octokit.Credentials("token")), new HttpClientAdapter(() => new Handler(this, reply)),
            new SimpleJsonSerializer()));

    /// <summary>2026-11-25-7a92a: REST calls beside GraphQL — answered by path ("GET repos/o/r/…"); a null reply is 404.</summary>
    public Func<string, (HttpStatusCode Status, string Body)?>? Rest { get; set; }

    private sealed class Handler(GitHubGraphQlHost host, Func<string, JsonElement, string> reply) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/graphql", StringComparison.Ordinal)) return Rest(request);
            using var posted = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var query = posted.RootElement.GetProperty("query").GetString()!;
            var variables = posted.RootElement.GetProperty("variables").Clone();
            host.Requests.Add((request.RequestUri!, query, variables));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply(query, variables), Encoding.UTF8, "application/json"),
            };
        }

        private HttpResponseMessage Rest(HttpRequestMessage request)
        {
            var call = $"{request.Method} {request.RequestUri!.AbsolutePath.TrimStart('/')}";
            host.Requests.Add((request.RequestUri, call, default));
            var (status, body) = host.Rest?.Invoke(call) ?? (HttpStatusCode.NotFound, """{"message":"Not Found"}""");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
