using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4da: who a project binding names — a group member or a person on the name
/// claim — and that what it grants is held in that one project only.
/// </summary>
public sealed class ProjectBindingTests
{
    private static readonly TokenAuthorityConfig Auth = new() { NameClaim = "preferred_username" };

    [Fact]
    public void Permissions_ProjectScopable_IsTheSixRunAndDialogNamesAndASubsetOfTheCatalog()
    {
        Permissions.ProjectScopable.Should().BeEquivalentTo(
            Permissions.RunsRead, Permissions.RunsWatch, Permissions.RunsControl,
            Permissions.RunsDelete, Permissions.ProjectsInit, Permissions.DialogWrite);
        Permissions.All.Should().Contain(Permissions.ProjectScopable);
    }

    [Fact]
    public void ProjectBinding_GroupMember_HoldsTheRoleInThatProjectOnly()
    {
        var identity = Resolve(Mapping(Binding("billing", groups: ["billing-team"])), ("groups", "billing-team"));

        identity.ProjectPermissions.Keys.Should().Equal("billing");
        identity.ProjectPermissions["billing"].Should().Equal(Permissions.RunsRead, Permissions.RunsWatch);
        identity.Permissions.Should().Equal(Permissions.IdentityRead);
        identity.Roles.Should().BeEmpty("a binding is not a global role");
    }

    [Fact]
    public void ProjectBinding_PersonRefOnTheNameClaim_HoldsTheRoleInThatProject()
    {
        var binding = Binding("billing", people: [new PersonRef { Claim = "preferred_username", Value = "ada" }]);

        Resolve(Mapping(binding), ("preferred_username", "ada"))
            .ProjectPermissions.Should().ContainKey("billing");
    }

    [Fact]
    public void ProjectBinding_PersonRefOnAnotherClaim_GrantsNothing()
    {
        var binding = Binding("billing", people: [new PersonRef { Claim = "email", Value = "ada" }]);

        Resolve(Mapping(binding), ("preferred_username", "ada"), ("email", "ada"))
            .ProjectPermissions.Should().BeEmpty("a value read under another claim may name somebody else");
    }

    [Fact]
    public void ProjectBinding_GroupWithALeadingSlash_MatchesTheUnprefixedValue()
    {
        Resolve(Mapping(Binding("billing", groups: ["/billing-team"])), ("groups", "billing-team"))
            .ProjectPermissions.Should().ContainKey("billing");
        Resolve(Mapping(Binding("billing", groups: ["billing-team"])), ("groups", "/billing-team"))
            .ProjectPermissions.Should().ContainKey("billing");
    }

    [Fact]
    public void ProjectBinding_BlankProject_IsIgnored()
    {
        Resolve(Mapping(Binding(" ", groups: ["billing-team"])), ("groups", "billing-team"))
            .ProjectPermissions.Should().BeEmpty();
    }

    [Fact]
    public void ProjectBinding_TwoBindingsOfOneProjectDifferingInCase_AreUnioned()
    {
        var identity = Resolve(
            Mapping(Binding("Billing", groups: ["readers"]),
                Binding("billing", groups: ["deployers"], roles: [BuiltInRoles.Operator])),
            ("groups", "readers"), ("groups", "deployers"));

        identity.ProjectPermissions.Should().ContainSingle();
        identity.PermissionsIn("BILLING").Should().Contain([Permissions.RunsControl, Permissions.RunsRead]);
    }

    [Fact]
    public void ProjectBinding_SavedInTheStore_ResolvesOnTheNextRequestWithoutARestart()
    {
        var stored = new StoredMappingStub(Mapping());
        var resolver = ResolverUnderTest.Over(Auth, stored);
        var caller = ResolverUnderTest.Caller(Auth, ("groups", "billing-team"));
        resolver.Resolve(caller).ProjectPermissions.Should().BeEmpty();

        stored.Stored = Mapping(Binding("billing", groups: ["billing-team"]));

        resolver.Resolve(caller).ProjectPermissions.Should().ContainKey("billing");
    }

    internal static AgentSmith.Server.Models.CallerIdentity Resolve(
        RoleMappingConfig mapping, params (string Type, string Value)[] claims) =>
        ResolverUnderTest.Over(Auth, new StoredMappingStub(mapping))
            .Resolve(ResolverUnderTest.Caller(Auth, claims));

    internal static RoleMappingConfig Mapping(params ProjectBinding[] bindings) =>
        new() { RoleClaim = "roles", GroupClaim = "groups", ProjectBindings = [.. bindings] };

    internal static ProjectBinding Binding(
        string project, List<string>? groups = null, List<PersonRef>? people = null, List<string>? roles = null) =>
        new()
        {
            Project = project, Groups = groups ?? [], People = people ?? [],
            Roles = roles ?? [BuiltInRoles.Reader],
        };
}
