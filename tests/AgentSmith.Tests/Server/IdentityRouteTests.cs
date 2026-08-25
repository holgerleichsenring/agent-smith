using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0503d: the identity endpoint is one row on p0503a's golden, and it rides the dashboard
/// gate. Mapped unconditionally it would break the guard test twice over — the eleven
/// routes left with the dashboard off would become twelve, and the assertion that all of
/// them are anonymous would be false along with its stated reason.
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class IdentityRouteTests
{
    private const string GateEnvVar = "AGENTSMITH_UI_API_ENABLED";
    private const string IdentityRoute = "/api/identity";

    [Fact]
    public void Identity_DashboardApiDisabled_TheRouteIsNotMapped()
    {
        var gated = WithGate("false", () => ServerRouteTable.Patterns(MapAsTheHostDoes));

        gated.Should().NotContain(IdentityRoute);
        WithGate("true", () => ServerRouteTable.Patterns(MapAsTheHostDoes)).Should().Contain(IdentityRoute);
    }

    // The golden is regenerated deliberately (AGENTSMITH_WRITE_ROUTE_BASELINE=1) and the
    // diff IS the review — so the review is written down: p0503a left 77 rows, and this
    // phase adds exactly one, stating identity.read like every other route.
    [Fact]
    public void RouteGuard_TheGolden_GainsExactlyTheIdentityRow()
    {
        var golden = File.ReadAllLines(Path.Combine(
                ArchitectureSources.TestSourceRoot, "Server", "route-permission-baseline.tsv"))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        // 2026-08-25-e257: the absolute count was the brittle half of this assertion — it
        // fails for every route anyone adds and says nothing about identity. The proof is in
        // its own history: it was 78, then 79 for the auth-requirements row, and would be 82
        // now. What it guards against is a golden regenerated wholesale and SHORT, which
        // survives as a floor; RoutePermissionGuardTests compares the whole table against the
        // live routes and is the stronger check either way.
        golden.Should().HaveCountGreaterThanOrEqualTo(79,
            "the golden may gain routes and must never silently lose the ones it had");
        golden.Where(row => row.Contains(IdentityRoute, StringComparison.Ordinal))
            .Should().ContainSingle().Which.Should().Be($"GET\t{IdentityRoute}\t{Permissions.IdentityRead}");
    }

    private static void MapAsTheHostDoes(WebApplication app)
    {
        app.MapServerEndpoints();
        if (DashboardApiExtensions.IsEnabled) app.MapDashboardApi();
    }

    private static T WithGate<T>(string value, Func<T> read)
    {
        var previous = Environment.GetEnvironmentVariable(GateEnvVar);
        Environment.SetEnvironmentVariable(GateEnvVar, value);
        try { return read(); }
        finally { Environment.SetEnvironmentVariable(GateEnvVar, previous); }
    }
}
