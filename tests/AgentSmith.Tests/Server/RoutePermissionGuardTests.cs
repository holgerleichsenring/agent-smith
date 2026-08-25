using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0503a: every route the server maps states which permission a caller needs, and a
/// route that states nothing fails here.
/// <para>
/// The assertion is a GOLDEN, not a count: a count is satisfied by the wrong permission
/// on the right route, so the table pins verb, pattern and permission list and a route
/// moving between permissions is a visible diff. Regenerate with
/// AGENTSMITH_WRITE_ROUTE_BASELINE=1 — and read the diff, because that is the review.
/// </para>
/// <para>
/// The environment is PINNED rather than inherited: DashboardApiExtensions.IsEnabled
/// reads AGENTSMITH_UI_API_ENABLED from the process on every call and caches nothing, so
/// a run that happened to see the dashboard API switched off would pass vacuously over
/// the unconditional routes alone. Hence the explicit set and the env-var collection.
/// </para>
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class RoutePermissionGuardTests
{
    private const string GateEnvVar = "AGENTSMITH_UI_API_ENABLED";
    private const string BaselineFile = "route-permission-baseline.tsv";
    private const string WriteFlag = "AGENTSMITH_WRITE_ROUTE_BASELINE";

    private static readonly string[] EntityBasePaths =
    [
        "agents", "trackers", "repos", "projects", "mcp-servers", "secrets", "connections",
    ];

    [Fact]
    public void RouteGuard_EveryMappedRoute_DeclaresAPermissionOrAnAnonymousReason()
    {
        var undeclared = Undeclared(WholeTable());

        undeclared.Should().BeEmpty(
            "a route that states nothing is a route nobody can authorize. Chain "
            + ".Needs(...) or .Anonymous(reason) onto the map call.\n  "
            + string.Join("\n  ", undeclared));
    }

    [Fact]
    public void RouteGuard_TheRouteTable_MatchesTheGoldenMethodPathAndPermissionList()
    {
        var rows = Rows(WholeTable());
        if (Environment.GetEnvironmentVariable(WriteFlag) == "1")
        {
            File.WriteAllText(BaselinePath(), string.Join("\n", rows) + "\n");
            return;
        }

        rows.Should().Equal(
            Golden(),
            "the route table IS the permission table. A route that moved between "
            + $"permissions shows up here; regenerate {BaselineFile} deliberately.");
    }

    [Fact]
    public void RouteGuard_ARouteAddedWithoutADeclaration_Fails()
    {
        var withAStowaway = Enumerate(app =>
        {
            MapAsTheHostDoes(app);
            app.MapGet("/api/runs/{runId}/stowaway", () => Results.Ok());
        });

        Undeclared(withAStowaway).Should().ContainSingle()
            .Which.Should().Contain("/api/runs/{runId}/stowaway");
    }

    // The twenty-eight routes MapEntity generates are the ones a hurried implementer
    // never touches, so they are named rather than counted "all covered".
    [Fact]
    public void RouteGuard_GeneratedEntityRoutes_AreTwentyEightAcrossTheSevenBasePaths()
    {
        var generated = WholeTable()
            .Where(fact => EntityBasePaths.Any(
                entity => fact.Pattern == $"/api/config/{entity}"
                          || fact.Pattern == $"/api/config/{entity}/{{id}}"))
            .ToList();

        generated.Should().HaveCount(28);
        generated.Should().OnlyContain(fact => fact.Permissions.Count == 1);
        generated.Select(fact => fact.Method).Distinct().Should()
            .BeEquivalentTo(["GET", "POST", "PUT", "DELETE"]);
    }

    [Fact]
    public void RouteGuard_TheSecretsEntityRoutes_CarryTheSecretsPermissions()
    {
        var secrets = WholeTable()
            .Where(fact => fact.Pattern.StartsWith("/api/config/secrets", StringComparison.Ordinal))
            .ToList();

        secrets.Should().HaveCount(4);
        secrets.Should().OnlyContain(fact =>
            fact.Permissions.Single() == Permissions.SecretsRead
            || fact.Permissions.Single() == Permissions.SecretsWrite);
        secrets.Should().NotContain(fact => fact.Permissions.Contains(Permissions.ConfigRead));
        secrets.Should().NotContain(fact => fact.Permissions.Contains(Permissions.ConfigWrite));
    }

    // The config/secrets split holds for direct entity CRUD and leaks here: the change
    // feed carries the secret's id and changed field names, revert takes no type filter
    // at all, export serializes the secret entities, and import writes them.
    [Fact]
    public void RouteGuard_TheChangeFeedRevertExportAndImport_CarryBothPermissions()
    {
        Declaration("GET", "/api/config/changes").Should()
            .Equal(Permissions.ConfigRead, Permissions.SecretsRead);
        Declaration("POST", "/api/config/changes/{id}/revert").Should()
            .Equal(Permissions.ConfigWrite, Permissions.SecretsWrite);
        Declaration("GET", "/api/config/export.yml").Should()
            .Equal(Permissions.ConfigExport, Permissions.SecretsRead);
        Declaration("POST", "/api/config/import").Should()
            .Equal(Permissions.ConfigImport, Permissions.SecretsWrite);
    }

    [Fact]
    public void RouteGuard_TheThirteenAnonymousRoutes_AreExactlyTheDeclaredSet()
    {
        var anonymous = WholeTable().Where(fact => fact.AnonymousReason is not null).ToList();

        anonymous.Select(fact => fact.Pattern).Should().BeEquivalentTo([
            "/health", "/api/config/findings", "/api/openapi.json",
            // 2026-08-25-4530: what the server expects of a caller, read by a caller who
            // has nothing to present — which is the state this route explains.
            "/api/auth/requirements",
            "/slack/events", "/slack/interact", "/slack/commands", "/slack/options",
            "/api/teams/messages",
            "/webhook", "/webhook/github", "/webhook/gitlab", "/webhook/jira",
        ]);
        anonymous.Should().OnlyContain(fact => fact.AnonymousReason!.Length > 0);
        anonymous.Should().OnlyContain(fact => fact.Permissions.Count == 0);
    }

    [Fact]
    public void RouteGuard_DashboardApiDisabled_EnumeratesOnlyTheUnconditionalRoutes()
    {
        var gated = WithGate("false", () => Enumerate(MapAsTheHostDoes));

        gated.Select(fact => fact.Pattern).Should().NotContain("/api/runs");
        gated.Select(fact => fact.Pattern).Should().Contain("/health");
        gated.Should().OnlyContain(fact => fact.AnonymousReason != null);
        gated.Should().HaveCount(12, "everything left with the dashboard off is a machine caller or a probe");
    }

    // The seam resolves nothing: poisoning the configuration loader would throw for any
    // enumeration that built a handler's parameters, and /api/runs/{runId}/retry takes an
    // AgentSmithConfig. No database, no Redis and no spawner answer "what is mapped?".
    [Fact]
    public void RouteGuard_TheRouteTable_EnumeratesWithoutADatabaseOrARedis()
    {
        var facts = WithGate("true", () => ServerRouteTable.Facts(
            MapAsTheHostDoes,
            services =>
            {
                services.RemoveAll<IConfigurationLoader>();
                services.AddSingleton<IConfigurationLoader, ExplodingConfigurationLoader>();
            }));

        Rows(facts).Should().Equal(Golden());
    }

    private static void MapAsTheHostDoes(WebApplication app)
    {
        // Exactly what ServerHostFactory does, gate included.
        app.MapServerEndpoints();
        if (DashboardApiExtensions.IsEnabled) app.MapDashboardApi();
    }

    private static IReadOnlyList<RouteFact> WholeTable() =>
        WithGate("true", () => Enumerate(MapAsTheHostDoes));

    private static IReadOnlyList<RouteFact> Enumerate(Action<WebApplication> map) =>
        ServerRouteTable.Facts(map);

    private static IReadOnlyList<string> Declaration(string method, string pattern) =>
        WholeTable().Single(fact => fact.Method == method && fact.Pattern == pattern).Permissions;

    private static IReadOnlyList<string> Undeclared(IEnumerable<RouteFact> facts) =>
        [.. facts.Where(fact => !fact.IsDeclared).Select(fact => $"{fact.Method} {fact.Pattern}")
            .OrderBy(row => row, StringComparer.Ordinal)];

    private static IReadOnlyList<string> Rows(IEnumerable<RouteFact> facts) =>
        [.. facts.Select(fact => fact.Row).OrderBy(row => row, StringComparer.Ordinal)];

    private static IReadOnlyList<string> Golden() =>
        [.. File.ReadAllLines(BaselinePath())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .OrderBy(line => line, StringComparer.Ordinal)];

    private static string BaselinePath() =>
        Path.Combine(ArchitectureSources.TestSourceRoot, "Server", BaselineFile);

    private static T WithGate<T>(string value, Func<T> read)
    {
        var previous = Environment.GetEnvironmentVariable(GateEnvVar);
        Environment.SetEnvironmentVariable(GateEnvVar, value);
        try { return read(); }
        finally { Environment.SetEnvironmentVariable(GateEnvVar, previous); }
    }

    private sealed class ExplodingConfigurationLoader : IConfigurationLoader
    {
        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath) =>
            throw new InvalidOperationException("the route table resolved a service");
    }
}
