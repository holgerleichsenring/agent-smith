using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4da: the identity answer carries what a binding grants beside the global list,
/// and <c>PermissionsIn</c> is the one place "global plus that project" is stated.
/// </summary>
public sealed class CallerIdentityProjectTests
{
    private static readonly TokenAuthorityConfig Auth = new() { NameClaim = "preferred_username" };

    [Fact]
    public void CallerIdentity_PermissionsIn_IsGlobalUnionProjectAndGlobalAloneForAnotherProject()
    {
        var mapping = Mapping(Binding("billing", groups: ["deployers"], roles: [BuiltInRoles.Operator]));
        mapping.GroupRoles["everyone"] = [BuiltInRoles.Reader];

        var identity = Resolve(mapping, ("groups", "deployers"), ("groups", "everyone"));

        identity.PermissionsIn("billing").Should().Contain([Permissions.RunsControl, Permissions.CatalogRead]);
        identity.PermissionsIn("billing").Should().BeInAscendingOrder(StringComparer.Ordinal);
        identity.PermissionsIn("payroll").Should().Equal(identity.Permissions);
        identity.PermissionsIn("payroll").Should().NotContain(Permissions.RunsControl);
    }

    [Fact]
    public void CallerIdentity_Anonymous_HasNoProjectPermissions()
    {
        var resolver = ResolverUnderTest.Over(
            Auth, new StoredMappingStub(Mapping(Binding("billing", groups: ["deployers"]))));

        var identity = resolver.Resolve(new ClaimsPrincipal(new ClaimsIdentity()));

        identity.ProjectPermissions.Should().BeEmpty();
        identity.PermissionsIn("billing").Should().BeEmpty();
    }

    [Fact]
    public void CallerIdentity_TokenPermissionClaimAndAdminGrant_StayGlobal()
    {
        var resolver = ResolverUnderTest.Over(
            Auth, new StoredMappingStub(Mapping()), grant: "sub:rescue-0001");

        var identity = resolver.Resolve(ResolverUnderTest.Caller(
            Auth, ("sub", "rescue-0001"), (PermissionClaims.Type, Permissions.RunsControl)));

        identity.ProjectPermissions.Should().BeEmpty();
        identity.Permissions.Should().Contain([Permissions.RunsControl, Permissions.ConfigWrite]);
    }

    [Fact]
    public void Identity_BindingThatGrantsNothing_IsNotNamedInAnyCallersFindings()
    {
        var mapping = Mapping(
            Binding("secret-project", groups: ["auditors"], roles: ["no-such-role"]),
            Binding("other-project", people: [new() { Claim = "email", Value = "ada" }]));

        var identity = Resolve(mapping, ("groups", "auditors"), ("preferred_username", "ada"));

        identity.ProjectPermissions.Should().BeEmpty();
        identity.Findings.Should().NotContain(f => f.Contains("secret-project") || f.Contains("other-project"));
    }
}
