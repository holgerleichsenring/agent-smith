using AgentSmith.Server.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503e: a server booted against an authority that is dead BEFORE the listener binds —
/// a loopback port nothing listens on. The issuer the base fixture starts is still there
/// to mint tokens with, which is the point: a caller can present a perfectly formed token
/// and the server still cannot check it against anything.
/// </summary>
public sealed class UnreachableAuthorityFixture : AuthorityFixture
{
    /// <summary>A loopback port nothing listens on — a refused connection, not a slow one.</summary>
    public const string DeadAuthority = "http://127.0.0.1:1";

    protected override string AuthYaml(string authority) => $"""
        auth:
          authority: {DeadAuthority}
          audience: {Audience}
          enforce: true
        """;

    /// <summary>
    /// One pass, on demand. The hosted schedule has almost certainly run one already by the
    /// time a test asks — "almost certainly" is not an assertion, so the test forces it.
    /// </summary>
    public Task ProbeOnceAsync() => Server.Services
        .GetRequiredService<IAuthorityReachability>().ProbeAsync(CancellationToken.None);
}
