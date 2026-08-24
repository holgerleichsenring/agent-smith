namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d: an enforcing authority whose auth block also says how a directory's claims
/// become roles — a role claim named <c>roles</c>, a group mapping, and one custom role.
/// The role claim's NAME is the point: the default inbound map rewrites <c>roles</c> to
/// the long WS-Federation role type, so this configuration is the one that fails in
/// production and passes in a unit test that never runs the handler.
/// </summary>
public sealed class RoleMappingAuthorityFixture : AuthorityFixture
{
    /// <summary>The group this installation maps, and the role it maps onto.</summary>
    public const string MappedGroup = "platform-operators";

    /// <summary>A role this installation added: config.read and nothing else.</summary>
    public const string CustomRole = "config-viewer";

    protected override string AuthYaml(string authority) => $"""
        auth:
          authority: {authority}
          audience: {Audience}
          enforce: true
          role_claim: roles
          group_claim: groups
          name_claim: sub
          group_roles:
            {MappedGroup}:
              - operator
          roles:
            {CustomRole}:
              - config.read
        """;
}
