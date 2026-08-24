namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: an authority configured with the enforce switch OFF — the state an installation
/// sits in while its operator prepares an identity provider and the dashboard still has no
/// way to sign in. Tokens are validated; nothing is refused.
/// </summary>
public sealed class PermissiveAuthorityFixture : AuthorityFixture
{
    protected override string AuthYaml(string authority) => $"""
        auth:
          authority: {authority}
          audience: {Audience}
          enforce: false
        """;
}
