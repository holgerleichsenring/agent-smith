using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: a registry request with the distribution API's anonymous auth flow — a
/// 401 carrying a Bearer challenge is answered by fetching a token from the challenge's realm
/// and sending once more under it. A request already carrying a token is not retried.
/// </summary>
public sealed class RegistryClient(
    RegistryTransport transport, RegistryChallengeParser challenges, RegistryTokenSource tokens)
{
    private const int Unauthorized = 401;

    public async Task<RegistryReply> SendAsync(
        HttpMethod method, Uri url, string accept, string? bearer, CancellationToken cancellationToken)
    {
        var reply = await transport.SendAsync(method, url, accept, bearer, cancellationToken);
        if (reply.Status != Unauthorized || bearer is not null
            || challenges.Parse(reply.Challenge) is not { } challenge)
            return reply;
        var token = await tokens.TokenAsync(challenge, cancellationToken);
        return token is null
            ? reply
            : await transport.SendAsync(method, url, accept, token, cancellationToken);
    }
}
