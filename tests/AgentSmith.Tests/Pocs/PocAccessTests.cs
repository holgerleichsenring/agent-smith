using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: acting on a PoC needs pocs.admin, or pocs.write plus runs.read granted by a BINDING of the PoC's
/// configured project — a global reader holding the poc role reads every PoC and acts only on their own.
/// </summary>
public sealed class PocAccessTests
{
    private static readonly TokenAuthorityConfig Auth = new() { Enforce = true, NameClaim = "preferred_username" };
    private static readonly string[] Read = [Permissions.RunsRead];

    [Fact]
    public void HoldsByBindingIn_ConfiguredBinding_Holds_GoneProjectOrGlobalAlone_DoesNot()
    {
        var mapping = Mapping(Binding("poc-ann", groups: ["ann"]), Binding("poc-gone", groups: ["ann"]));
        mapping.GroupRoles["readers"] = [BuiltInRoles.Reader];
        var access = Access(mapping, "poc-ann");

        access.HoldsByBindingIn(Caller("ann"), Read, "POC-ANN").Should().BeTrue("project names fold case");
        access.HoldsByBindingIn(Caller("ann"), Read, "poc-gone").Should().BeFalse("the project left the configuration");
        access.HoldsByBindingIn(Caller("readers"), Read, "poc-ann").Should().BeFalse("a global hold is not a binding");
    }

    [Fact]
    public void MayAct_OwnBindingAndPocsWrite_Acts_GlobalReaderOnAnothersPoc_DoesNot()
    {
        var mapping = Mapping(Binding("poc-ann", groups: ["ann"]), Binding("poc-bob", groups: ["bob"]));
        mapping.GroupRoles["ann"] = [BuiltInRoles.Poc, BuiltInRoles.Reader];
        mapping.GroupRoles["bob"] = [BuiltInRoles.Poc];
        var acts = new PocActAccess(Access(mapping, "poc-ann", "poc-bob"));

        acts.MayAct(Caller("ann"), PocRows.Poc("todo", "poc-ann")).Should().BeTrue();
        acts.MayAct(Caller("ann"), PocRows.Poc("shop", "poc-bob")).Should().BeFalse(
            "global reader shows ann bob's PoC, it does not let her act on it");
        acts.MayAct(Caller("nobody"), PocRows.Poc("todo", "poc-ann")).Should().BeFalse();
    }

    [Fact]
    public void MayAct_PocsAdmin_ActsEverywhere_NothingEnforced_EveryoneActs()
    {
        var mapping = Mapping();
        mapping.GroupRoles["admins"] = [BuiltInRoles.Admin];

        new PocActAccess(Access(mapping, "poc-ann")).MayAct(Caller("admins"), PocRows.Poc("todo", "poc-ann")).Should().BeTrue();
        new PocActAccess(ProjectScopeUnderTest.Access(new TokenAuthorityConfig(), null, "poc-ann"))
            .MayAct(new ClaimsPrincipal(), PocRows.Poc("todo", "poc-ann")).Should().BeTrue();
    }

    private static ProjectAccess Access(RoleMappingConfig mapping, params string[] configured) =>
        ProjectScopeUnderTest.Access(Auth, ResolverUnderTest.Over(Auth, new StoredMappingStub(mapping)), configured);

    private static ClaimsPrincipal Caller(string group) => ResolverUnderTest.Caller(Auth, ("groups", group));
}
