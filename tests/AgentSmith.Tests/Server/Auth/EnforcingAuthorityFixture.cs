namespace AgentSmith.Tests.Server.Auth;

/// <summary>p0503b: an authority, an audience, and the enforce switch on.</summary>
public sealed class EnforcingAuthorityFixture : AuthorityFixture
{
    protected override string AuthYaml(string authority) => $"""
        auth:
          authority: {authority}
          audience: {Audience}
          enforce: true
        """;
}
