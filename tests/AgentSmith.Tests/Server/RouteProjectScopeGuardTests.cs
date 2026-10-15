using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-15-4a4db: every route needing a project-scopable permission says where its project comes
/// from, so a caller who holds it only in other projects can be answered as if the resource did not
/// exist. Pinned like <see cref="RoutePermissionGuardTests"/>: the dashboard gate is SET, or the guard
/// would pass vacuously over the unconditional routes alone.
/// </summary>
[Trait("TestProcess", "env-3")]
[Collection(EnvVarCollection.Name)]
public sealed class RouteProjectScopeGuardTests
{
    private const string GateEnvVar = "AGENTSMITH_UI_API_ENABLED";

    private static readonly ProjectSourceKind[] RouteValueKinds =
        [ProjectSourceKind.Route, ProjectSourceKind.Run, ProjectSourceKind.Session, ProjectSourceKind.Image];

    [Fact]
    public void RouteProjectScopeGuard_EveryProjectScopableRoute_DeclaresWhereItsProjectComesFrom()
    {
        var unscoped = Unscoped(Table(MapAsTheHostDoes));

        unscoped.Should().BeEmpty(
            "a route with a project-scopable permission chains .InProject(ProjectFrom...) onto its map call.\n  "
            + string.Join("\n  ", unscoped));
    }

    [Fact]
    public void RouteProjectScopeGuard_ARouteWithOnlyGlobalPermissions_DeclaresNoProject() =>
        Table(MapAsTheHostDoes)
            .Where(fact => !fact.Permissions.Any(Permissions.ProjectScopable.Contains))
            .Where(fact => fact.Scope is not null)
            .Select(fact => $"{fact.Method} {fact.Pattern}")
            .Should().BeEmpty("config, secrets and the rest of the catalog are global-only");

    [Fact]
    public void RouteProjectScopeGuard_ARouteValueSource_NamesAParameterInItsPattern() =>
        Table(MapAsTheHostDoes)
            .Where(fact => fact.Scope is { } scope && RouteValueKinds.Contains(scope.Kind))
            .Where(fact => !fact.Pattern.Contains($"{{{fact.Scope!.Parameter}}}", StringComparison.Ordinal)
                           && !fact.Pattern.Contains($"{{{fact.Scope.Parameter}:", StringComparison.Ordinal))
            .Select(fact => $"{fact.Method} {fact.Pattern} {fact.Scope!.Wire}")
            .Should().BeEmpty("a source read from a route value needs that value in the pattern");

    [Fact]
    public void RouteProjectScopeGuard_Body_IsDeclaredOnTheMessageRouteAlone() =>
        Table(MapAsTheHostDoes)
            .Where(fact => fact.Scope?.Kind == ProjectSourceKind.Body)
            .Select(fact => $"{fact.Method} {fact.Pattern}")
            .Should().Equal(["POST /api/spec-dialog/messages"],
                "a body source is checked by its handler, which only the message route does");

    [Fact]
    public void RouteProjectScopeGuard_TheHubKind_IsDeclaredOnTheHubRouteAlone() =>
        Table(MapAsTheHostDoes)
            .Where(fact => fact.Scope?.Kind == ProjectSourceKind.Hub)
            .Select(fact => fact.Pattern).Order(StringComparer.Ordinal)
            .Should().Equal(["/hub/jobs", "/hub/jobs/negotiate"],
                "2026-10-15-4a4dc: only the hub's filter scopes every invocation behind an any-project admission");

    [Fact]
    public void RouteProjectScopeGuard_AStowawayRunRouteWithoutAScope_Fails()
    {
        var withAStowaway = Table(app =>
        {
            MapAsTheHostDoes(app);
            app.MapGet("/api/runs/{runId}/stowaway", () => Results.Ok()).Needs(Permissions.RunsRead);
        });

        Unscoped(withAStowaway).Should().ContainSingle()
            .Which.Should().Contain("/api/runs/{runId}/stowaway");
    }

    private static IReadOnlyList<string> Unscoped(IEnumerable<RouteFact> facts) =>
        [.. facts.Where(fact => fact.Permissions.Any(Permissions.ProjectScopable.Contains) && fact.Scope is null)
            .Select(fact => $"{fact.Method} {fact.Pattern}").Order(StringComparer.Ordinal)];

    private static void MapAsTheHostDoes(WebApplication app)
    {
        app.MapServerEndpoints();
        if (DashboardApiExtensions.IsEnabled) app.MapDashboardApi();
    }

    private static IReadOnlyList<RouteFact> Table(Action<WebApplication> map)
    {
        var previous = Environment.GetEnvironmentVariable(GateEnvVar);
        Environment.SetEnvironmentVariable(GateEnvVar, "true");
        try { return ServerRouteTable.Facts(map); }
        finally { Environment.SetEnvironmentVariable(GateEnvVar, previous); }
    }
}
