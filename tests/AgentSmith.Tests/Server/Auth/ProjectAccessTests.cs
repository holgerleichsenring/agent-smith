using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: what every enforcement site asks — global holds everywhere, a binding's hold in its
/// configured project only, and a project the configuration no longer names through global holds alone.
/// </summary>
public sealed class ProjectAccessTests
{
    private static readonly TokenAuthorityConfig Auth = new() { Enforce = true, NameClaim = "preferred_username" };

    private static readonly string[] Control = [Permissions.RunsControl];

    [Fact]
    public void ProjectAccess_HoldsIn_GlobalUnionTheProjectsPermissions()
    {
        var mapping = Mapping(Binding("alpha", groups: ["alpha-team"], roles: [BuiltInRoles.Operator]));
        mapping.GroupRoles["everyone"] = [BuiltInRoles.Reader];
        var access = Access(mapping, "alpha", "beta");
        var caller = Caller(("groups", "alpha-team"), ("groups", "everyone"));

        access.HoldsIn(caller, [Permissions.RunsRead, Permissions.RunsControl], "alpha").Should().BeTrue(
            "runs.read is global and runs.control is alpha's");
        access.HoldsIn(caller, [Permissions.RunsRead, Permissions.RunsControl], "beta").Should().BeFalse();
        access.HoldsIn(caller, [Permissions.RunsRead], "beta").Should().BeTrue("a global hold reaches every project");
        access.HoldsIn(caller, Control, "ALPHA").Should().BeTrue("project names fold case");
    }

    [Fact]
    public void ProjectAccess_HoldsIn_AProjectNoLongerConfigured_OnlyThroughGlobal()
    {
        var mapping = Mapping(Binding("gone", groups: ["gone-team"], roles: [BuiltInRoles.Operator]));
        mapping.GroupRoles["admins"] = [BuiltInRoles.Admin];
        var access = Access(mapping, "alpha");

        access.HoldsIn(Caller(("groups", "gone-team")), Control, "gone").Should().BeFalse(
            "a binding to a project the configuration no longer names reaches nothing");
        access.HoldsIn(Caller(("groups", "admins")), Control, "gone").Should().BeTrue();
    }

    [Fact]
    public void ProjectAccess_HoldsAnywhereAndEverywhere_TellABindingFromAGlobalHold()
    {
        var access = Access(Mapping(Binding("alpha", groups: ["alpha-team"], roles: [BuiltInRoles.Operator])), "alpha");
        var caller = Caller(("groups", "alpha-team"));

        access.HoldsAnywhere(caller, Permissions.RunsControl).Should().BeTrue();
        access.HoldsEverywhere(caller, Control).Should().BeFalse();
        access.HoldsAnywhere(caller, Permissions.ConfigRead).Should().BeFalse("a binding never carries a global-only permission");
        access.Enforcing.Should().BeTrue();
    }

    private static ProjectAccess Access(RoleMappingConfig mapping, params string[] configured) =>
        ProjectScopeUnderTest.Access(Auth, ResolverUnderTest.Over(Auth, new StoredMappingStub(mapping)), configured);

    private static ClaimsPrincipal Caller(params (string Type, string Value)[] claims) =>
        ResolverUnderTest.Caller(Auth, claims);
}
