using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dd: the projects a list holds for a caller — everything off enforcement and for a global hold,
/// else the configured projects whose binding carries the asked permission, read off ONE configuration load.
/// </summary>
public sealed class CallerProjectScopeTests
{
    private static readonly TokenAuthorityConfig Enforcing = new() { Enforce = true, NameClaim = "preferred_username" };

    [Fact]
    public void CallerProjectScope_EnforcementOff_IsEverything()
    {
        var off = new TokenAuthorityConfig { NameClaim = "preferred_username" };
        var scope = Scope(off, Mapping(Binding("alpha", groups: ["alpha-team"], roles: [BuiltInRoles.Operator])), "alpha", "beta");

        scope.For(Caller(off, ("groups", "alpha-team")), Permissions.RunsRead).Only.Should().BeNull();
    }

    [Fact]
    public void CallerProjectScope_GlobalPermission_IsEverything()
    {
        var mapping = Mapping();
        mapping.GroupRoles["everyone"] = [BuiltInRoles.Reader];

        Scope(Enforcing, mapping, "alpha", "beta").For(Caller(Enforcing, ("groups", "everyone")), Permissions.RunsRead)
            .Only.Should().BeNull();
    }

    [Fact]
    public void CallerProjectScope_ProjectBinding_IsThoseConfiguredProjectsInConfiguredSpelling()
    {
        var mapping = Mapping(Binding("ALPHA", groups: ["alpha-team"], roles: [BuiltInRoles.Reader]));

        Scope(Enforcing, mapping, "alpha", "beta").For(Caller(Enforcing, ("groups", "alpha-team")), Permissions.RunsRead)
            .Only.Should().Equal("alpha");
    }

    [Fact]
    public void CallerProjectScope_BindingToAnUnconfiguredProject_AdmitsNothingThere()
    {
        var mapping = Mapping(Binding("gone", groups: ["gone-team"], roles: [BuiltInRoles.Reader]));

        Scope(Enforcing, mapping, "alpha").For(Caller(Enforcing, ("groups", "gone-team")), Permissions.RunsRead)
            .Only.Should().BeEmpty();
    }

    [Fact]
    public void CallerProjectScope_BindingWithoutTheAskedPermission_AdmitsNothingThere()
    {
        var mapping = Mapping(Binding("alpha", groups: ["alpha-team"], roles: [BuiltInRoles.Reader]));

        Scope(Enforcing, mapping, "alpha").For(Caller(Enforcing, ("groups", "alpha-team")), Permissions.RunsDelete)
            .Only.Should().BeEmpty("a reader binding carries no runs.delete");
    }

    [Fact]
    public void CallerProjectScope_ManyConfiguredProjects_LoadsTheConfigurationOnce()
    {
        var loader = new CountingLoader(ProjectScopeUnderTest.Configured("alpha", "beta", "gamma", "delta"));
        var mapping = Mapping(
            Binding("alpha", groups: ["team"], roles: [BuiltInRoles.Reader]),
            Binding("gamma", groups: ["team"], roles: [BuiltInRoles.Reader]));
        var access = new ProjectAccess(Enforcing, ResolverUnderTest.Over(Enforcing, new StoredMappingStub(mapping)),
            new TicketProjects(loader, new ServerContext("agentsmith.yml")));

        new CallerProjectScope(access).For(Caller(Enforcing, ("groups", "team")), Permissions.RunsRead)
            .Only.Should().Equal("alpha", "gamma");
        loader.Loads.Should().Be(1, "the filter is built from one read of the configuration, not one per project");
    }

    [Fact]
    public void CallerProjectScope_KeysAlreadyLoaded_LoadsNothing()
    {
        var loader = new CountingLoader(ProjectScopeUnderTest.Configured("alpha"));
        var mapping = Mapping(Binding("alpha", groups: ["team"], roles: [BuiltInRoles.Operator]));
        var access = new ProjectAccess(Enforcing, ResolverUnderTest.Over(Enforcing, new StoredMappingStub(mapping)),
            new TicketProjects(loader, new ServerContext("agentsmith.yml")));

        new CallerProjectScope(access).For(Caller(Enforcing, ("groups", "team")), Permissions.DialogWrite, ["alpha", "beta"])
            .Only.Should().Equal("alpha");
        loader.Loads.Should().Be(0);
    }

    private static CallerProjectScope Scope(TokenAuthorityConfig auth, RoleMappingConfig mapping, params string[] configured) =>
        new(ProjectScopeUnderTest.Access(auth, ResolverUnderTest.Over(auth, new StoredMappingStub(mapping)), configured));

    private static ClaimsPrincipal Caller(TokenAuthorityConfig auth, params (string Type, string Value)[] claims) =>
        ResolverUnderTest.Caller(auth, claims);

    internal sealed class CountingLoader(AgentSmithConfig config) : IConfigurationLoader
    {
        public int Loads { get; private set; }

        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath)
        {
            Loads++;
            return config;
        }
    }
}
