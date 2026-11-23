using System.Net;
using System.Net.Http.Headers;
using AgentSmith.Contracts.Exceptions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Registry;

/// <summary>
/// 2026-11-23-933de: HEADs manifests on an OCI distribution registry with a pull token its caller resolved. Unsigned
/// first; a 401 Bearer challenge has its realm checked against the login server before the Basic pull credentials go
/// to it, then the manifest is asked for again with the bearer token. Logs and refusals name registry, repository and
/// HTTP status only — never a header, a token or a body.
/// </summary>
public sealed class OciDistributionClient(
    HttpClient http, string loginServer, string pullUser, string pullToken, ILogger logger)
{
    public string LoginServer => loginServer;

    /// <summary>The manifest of <paramref name="repository"/> at a tag or digest.</summary>
    public async Task<OciManifestHead> HeadManifestAsync(string repository, string reference, CancellationToken cancellationToken)
    {
        using var first = await SendAsync(repository, reference, bearer: null, cancellationToken);
        if (first.StatusCode != HttpStatusCode.Unauthorized) return Read(first, repository);
        var challenge = OciChallenge.Parse(first)
            ?? throw new ContainerRegistryException($"registry {loginServer} answered 401 for {repository} without a Bearer challenge");
        challenge.EnsureOn(loginServer);
        var token = await new OciTokenExchange(http, loginServer, pullUser, pullToken)
            .RequestAsync(challenge, repository, cancellationToken);
        if (token is null)
        {
            logger.LogInformation("Registry {Registry} refused the pull token for {Repository}", loginServer, repository);
            return new OciManifestHead(OciManifestState.TokenRefused, (int)HttpStatusCode.Unauthorized);
        }
        using var second = await SendAsync(repository, reference, token, cancellationToken);
        return Read(second, repository);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string repository, string reference, string? bearer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head,
            new Uri($"https://{loginServer}/v2/{repository}/manifests/{reference}"));
        foreach (var type in OciMediaTypes.Accepted) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(type));
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ContainerRegistryException(
                $"registry {loginServer} could not be reached for {repository} ({ex.HttpRequestError})");
        }
    }

    private OciManifestHead Read(HttpResponseMessage response, string repository)
    {
        var status = (int)response.StatusCode;
        logger.LogDebug("Registry {Registry} answered {Status} for a manifest of {Repository}", loginServer, status, repository);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => new OciManifestHead(OciManifestState.Found, status, DigestOf(response)),
            HttpStatusCode.NotFound => new OciManifestHead(OciManifestState.Missing, status),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new OciManifestHead(OciManifestState.Denied, status),
            _ => throw new ContainerRegistryException($"registry {loginServer} answered status {status} for {repository}"),
        };
    }

    private static string? DigestOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Docker-Content-Digest", out var values) ? values.FirstOrDefault() : null;
}
