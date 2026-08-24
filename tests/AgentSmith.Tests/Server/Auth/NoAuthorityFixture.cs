namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: no auth block at all — every installation that existed before this phase. It is
/// not a defect and must not be reported as one.
/// </summary>
public sealed class NoAuthorityFixture : AuthorityFixture
{
    protected override string AuthYaml(string authority) => string.Empty;
}
