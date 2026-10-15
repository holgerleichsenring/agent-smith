using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dd: ticket search, resolve and pick read the trackers of the configuration they are handed — a
/// scoped caller is handed only their projects and the trackers those projects name.
/// </summary>
public sealed class ProjectScopedCatalogTests
{
    private static readonly TokenAuthorityConfig Enforcing = new() { Enforce = true, NameClaim = "preferred_username" };

    private static readonly AgentSmithConfig Loaded = new()
    {
        Projects = new Dictionary<string, ResolvedProject>(ConfigNames.Comparer)
        {
            ["alpha"] = Project("alpha", "board-a"),
            ["shared"] = Project("shared", "board-a"),
            ["beta"] = Project("beta", "board-b"),
        },
        Trackers = new Dictionary<string, TrackerConnection>
        {
            ["board-a"] = new() { Name = "board-a" },
            ["board-b"] = new() { Name = "board-b" },
        },
    };

    [Fact]
    public void TicketSearch_ScopedCaller_AsksOnlyTrackersOfTheirProjects()
    {
        var narrowed = Catalog(Operator("alpha")).Narrow(Loaded, Caller());

        narrowed.Trackers.Keys.Should().Equal("board-a");
    }

    [Fact]
    public void TicketPick_ScopedCaller_NamesOnlyTheirProjects()
    {
        var narrowed = Catalog(Operator("alpha")).Narrow(Loaded, Caller());

        narrowed.Projects.Keys.Should().Equal("alpha");
        narrowed.Projects.ContainsKey("ALPHA").Should().BeTrue("the narrowed catalogue keeps the name rule");
    }

    [Fact]
    public void ProjectScopedCatalog_GlobalHolder_IsTheLoadedConfigurationItself()
    {
        var mapping = Mapping();
        mapping.GroupRoles["team"] = [BuiltInRoles.Operator];

        Catalog(mapping).Narrow(Loaded, Caller()).Should().BeSameAs(Loaded);
    }

    private static RoleMappingConfig Operator(string project) =>
        Mapping(Binding(project, groups: ["team"], roles: [BuiltInRoles.Operator]));

    private static ProjectScopedCatalog Catalog(RoleMappingConfig mapping) =>
        new(new CallerProjectScope(ProjectScopeUnderTest.Access(
            Enforcing, ResolverUnderTest.Over(Enforcing, new StoredMappingStub(mapping)), "alpha", "shared", "beta")));

    private static ClaimsPrincipal Caller() => ResolverUnderTest.Caller(Enforcing, ("groups", "team"));

    private static ResolvedProject Project(string name, string tracker) =>
        new() { Name = name, Tracker = new TrackerConnection { Name = tracker } };
}
