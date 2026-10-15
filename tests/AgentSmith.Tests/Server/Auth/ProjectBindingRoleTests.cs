using AgentSmith.Server.Security;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4da: a binding's roles are cut down to the project-scopable permissions. The
/// rest of a bundle is global business and is dropped without a word, because every built-in
/// bundle carries some of it.
/// </summary>
public sealed class ProjectBindingRoleTests
{
    [Fact]
    public void ProjectBinding_OperatorRole_KeepsOnlyProjectScopablePermissions()
    {
        var identity = Resolve(
            Mapping(Binding("billing", groups: ["deployers"], roles: [BuiltInRoles.Operator])),
            ("groups", "deployers"));

        identity.ProjectPermissions["billing"].Should().BeEquivalentTo(
            Permissions.RunsRead, Permissions.RunsWatch, Permissions.RunsControl,
            Permissions.RunsDelete, Permissions.ProjectsInit, Permissions.DialogWrite);
        identity.ProjectPermissions["billing"].Should().NotContain(
            [Permissions.DiagnosticsProbe, Permissions.CatalogRead, Permissions.DiagnosticsRead]);
    }

    [Fact]
    public void ProjectBinding_AdminRole_GrantsEveryProjectScopablePermissionAndNoConfigRead()
    {
        var identity = Resolve(
            Mapping(Binding("billing", groups: ["owners"], roles: [BuiltInRoles.Admin])),
            ("groups", "owners"));

        identity.ProjectPermissions["billing"].Should().BeEquivalentTo(Permissions.ProjectScopable);
        identity.PermissionsIn("billing").Should().NotContain(
            [Permissions.ConfigRead, Permissions.SecretsRead, Permissions.AccessWrite]);
        identity.Roles.Should().NotContain(BuiltInRoles.Admin);
    }

    [Fact]
    public void ProjectBinding_RolesWithNoScopablePermission_GrantNothing()
    {
        var mapping = Mapping(Binding("billing", groups: ["auditors"], roles: ["auditor", "no-such-role"]));
        mapping.Roles["auditor"] = [Permissions.ConfigRead, Permissions.CatalogRead];

        Resolve(mapping, ("groups", "auditors")).ProjectPermissions.Should().BeEmpty();
    }
}
