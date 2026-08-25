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

        golden.Should().HaveCount(79, "p0503a's table was 77 rows, p0503d added the identity "
            + "row and 2026-08-25-4530 added the auth-requirements row");
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
