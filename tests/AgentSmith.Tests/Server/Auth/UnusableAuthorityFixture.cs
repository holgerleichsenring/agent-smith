namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: the block an operator THINKS they configured. The YAML loader ignores unmatched
/// properties, so the misspelled key below yields no authority, no parse error, and a
/// dashboard that quietly stays open to anyone.
/// </summary>
public sealed class UnusableAuthorityFixture : AuthorityFixture
{
    protected override string AuthYaml(string authority) => $"""
        auth:
          authorityy: {authority}
          enforce: true
        """;
}
