using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dd: GET /api/projects — the names of the projects a caller may read, a bare array in name order,
/// built from one configuration load per request.
/// </summary>
public sealed class ProjectListEndpointsTests
{
    private static readonly TokenAuthorityConfig Enforcing = new() { Enforce = true, NameClaim = "preferred_username" };

    [Fact]
    public void ProjectList_ScopedCaller_NamesOnlyTheirProjects()
    {
        var (scopes, projects, _) = Rig(Enforcing, Mapping(
            Binding("gamma", groups: ["team"], roles: [BuiltInRoles.Reader]),
            Binding("Alpha", groups: ["team"], roles: [BuiltInRoles.Reader])));

        Names(ProjectListEndpoints.List(Caller(Enforcing), scopes, projects)).Should().Equal("alpha", "gamma");
    }

    [Fact]
    public void ProjectList_EnforcementOff_NamesEveryConfiguredProject()
    {
        var off = new TokenAuthorityConfig { NameClaim = "preferred_username" };
        var (scopes, projects, _) = Rig(off, Mapping());

        Names(ProjectListEndpoints.List(new ClaimsPrincipal(), scopes, projects)).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void ProjectList_ScopedCaller_LoadsTheConfigurationOncePerRequest()
    {
        var (scopes, projects, loader) = Rig(Enforcing, Mapping(
            Binding("alpha", groups: ["team"], roles: [BuiltInRoles.Reader]),
            Binding("beta", groups: ["team"], roles: [BuiltInRoles.Reader])));

        ProjectListEndpoints.List(Caller(Enforcing), scopes, projects);

        loader.Loads.Should().Be(1);
    }

    private static (CallerProjectScope Scopes, TicketProjects Projects, CallerProjectScopeTests.CountingLoader Loader) Rig(
        TokenAuthorityConfig auth, RoleMappingConfig mapping)
    {
        var loader = new CallerProjectScopeTests.CountingLoader(ProjectScopeUnderTest.Configured("gamma", "alpha", "beta"));
        var projects = new TicketProjects(loader, new ServerContext("agentsmith.yml"));
        var access = new ProjectAccess(auth, ResolverUnderTest.Over(auth, new StoredMappingStub(mapping)), projects);
        return (new CallerProjectScope(access), projects, loader);
    }

    private static ClaimsPrincipal Caller(TokenAuthorityConfig auth) => ResolverUnderTest.Caller(auth, ("groups", "team"));

    private static IReadOnlyList<string> Names(IResult result) =>
        result.Should().BeOfType<Ok<IReadOnlyList<string>>>().Which.Value!;
}
