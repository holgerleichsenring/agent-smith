using System.Net;
using System.Text;
using AgentSmith.Infrastructure.Services.Providers.Registry;

namespace AgentSmith.Tests.Pocs.Registry;

/// <summary>
/// 2026-11-23-933de: an ACR-shaped registry over HTTP. An unsigned manifest HEAD answers 401 with a Bearer challenge
/// naming <see cref="Realm"/>; the realm issues a bearer for the Basic pull credentials; a signed HEAD answers a pushed
/// manifest's digest only when Accept names its media type, 401 outside the token's scope map, 404 otherwise. Every
/// request is recorded with its host, Authorization header and Accept.
/// </summary>
internal sealed class FakeRegistryHost : HttpMessageHandler
{
    public const string LoginServer = "pocregistry.azurecr.io";
    public const string PullUser = "poc-pull";
    public const string PullToken = "pull-secret-value-0001";
    public const string Bearer = "bearer-token-value-0002";

    public string Realm { get; set; } = $"https://{LoginServer}/oauth2/token";

    /// <summary>The repository prefix the token's scope map grants pull on.</summary>
    public string ScopeMap { get; set; } = "pocs/";

    public bool RefusesToken { get; set; }

    /// <summary>repository:reference → (digest, media type).</summary>
    public Dictionary<string, (string Digest, string MediaType)> Manifests { get; } = [];

    public List<(string Method, Uri Url, string Auth, string Accept)> Requests { get; } = [];

    public HttpClient Client() => new(this, disposeHandler: false);

    public void Push(string repository, string tag, string digest, string mediaType = OciMediaTypes.DockerManifest)
    {
        Manifests[$"{repository}:{tag}"] = (digest, mediaType);
        Manifests[$"{repository}:{digest}"] = (digest, mediaType);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = request.Headers.Authorization?.ToString() ?? string.Empty;
        Requests.Add((request.Method.Method, request.RequestUri!, auth, string.Join(",", request.Headers.Accept)));
        var path = request.RequestUri!.AbsolutePath;
        return Task.FromResult(path.StartsWith("/v2/", StringComparison.Ordinal)
            ? Manifest(path, request, auth)
            : TokenAnswer(request, auth));
    }

    private HttpResponseMessage TokenAnswer(HttpRequestMessage request, string auth)
    {
        var basic = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{PullUser}:{PullToken}"));
        if (RefusesToken || auth != basic)
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent($"denied {auth}") };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($$"""{"access_token":"{{Bearer}}"}""") };
    }

    private HttpResponseMessage Manifest(string path, HttpRequestMessage request, string auth)
    {
        var parts = path["/v2/".Length..].Split("/manifests/");
        var (repository, reference) = (parts[0], parts[1]);
        if (auth != $"Bearer {Bearer}")
        {
            var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            challenge.Headers.TryAddWithoutValidation("WWW-Authenticate",
                $"Bearer realm=\"{Realm}\",service=\"{LoginServer}\"");
            return challenge;
        }
        if (!repository.StartsWith(ScopeMap, StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent($"scope {auth}") };
        if (!Manifests.TryGetValue($"{repository}:{reference}", out var manifest)
            || !request.Headers.Accept.Any(a => a.MediaType == manifest.MediaType))
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        var found = new HttpResponseMessage(HttpStatusCode.OK);
        found.Headers.TryAddWithoutValidation("Docker-Content-Digest", manifest.Digest);
        return found;
    }
}
