using System.Net;
using System.Text;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Sandbox.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: an image registry and its token service without the network — every
/// request the lookup makes is answered by <see cref="Answer"/> and recorded, and every host
/// resolves to <see cref="Address"/> for the public-address guard.
/// </summary>
internal sealed class FakeRegistry : HttpMessageHandler, IHttpClientFactory, IHostAddressResolver
{
    public const string Token = "anon-token";

    public List<HttpRequestMessage> Requests { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage> Answer { get; set; } = _ => Respond(HttpStatusCode.OK);

    public IPAddress Address { get; set; } = IPAddress.Parse("93.184.216.34");

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Task.FromResult(new[] { Address });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Answer(request));
    }

    public ImageReferenceCheck Check()
    {
        var transport = new RegistryTransport(
            this, new RenderUrlGuard(this, new PublicAddressRule()), NullLogger<RegistryTransport>.Instance);
        var client = new RegistryClient(
            transport, new RegistryChallengeParser(),
            new RegistryTokenSource(transport, NullLogger<RegistryTokenSource>.Instance));
        var lister = new RegistryTagLister(client, new RegistryTagPageParser(NullLogger<RegistryTagPageParser>.Instance));
        return new ImageReferenceCheck(new RegistryImageReferenceParser(), client, lister, new NearestTagRanker());
    }

    /// <summary>Docker Hub's shape: anonymous 401 with a challenge, a token, then <paramref name="authorized"/>.</summary>
    public static Func<HttpRequestMessage, HttpResponseMessage> DockerHub(
        Func<HttpRequestMessage, HttpResponseMessage> authorized) => request =>
    {
        if (request.RequestUri!.Host == "auth.docker.io")
            return Respond(HttpStatusCode.OK, $$"""{"token":"{{Token}}"}""");
        if (request.Headers.Authorization?.Parameter != Token)
        {
            var challenge = Respond(HttpStatusCode.Unauthorized);
            challenge.Headers.TryAddWithoutValidation("WWW-Authenticate",
                "Bearer realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\","
                + "scope=\"repository:sbtscala/scala-sbt:pull\"");
            return challenge;
        }
        return authorized(request);
    };

    public static HttpResponseMessage Respond(HttpStatusCode status, string body = "") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Tags(string link, params string[] tags)
    {
        var response = Respond(HttpStatusCode.OK,
            "{\"name\":\"sbtscala/scala-sbt\",\"tags\":[" + string.Join(",", tags.Select(t => $"\"{t}\"")) + "]}");
        if (link.Length > 0) response.Headers.TryAddWithoutValidation("Link", link);
        return response;
    }
}
