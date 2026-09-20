using AgentSmith.Server.Extensions;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;

namespace AgentSmith.Tests.Server.Catalog;

/// <summary>
/// 2026-09-18-84be: the catalog page tells its reader that nothing is edited there and
/// that the source is changed in the Skills setting. A rendered sentence only proves
/// someone typed it; what keeps the sentence TRUE is that the catalog prefix carries no
/// write route. This enumerates the mapped routes — over the real server composition,
/// resolving no service — and fails the day a non-GET route appears under the prefix.
/// <para>
/// BOTH chains are mapped, the way the host maps them: a write route added to the
/// unconditional <c>MapServerEndpoints</c> chain would never reach a dashboard-only
/// enumeration, and it is the chain that survives with the dashboard API switched off.
/// </para>
/// </summary>
public sealed class CatalogRouteGuardTests
{
    private const string CatalogPrefix = "/api/catalog";

    [Fact]
    public void Catalog_EveryRouteUnderThePrefix_IsAGet()
    {
        var facts = ServerRouteTable
            .Facts(app => app.MapServerEndpoints().MapDashboardApi())
            .Where(fact => fact.Pattern.StartsWith(CatalogPrefix, StringComparison.Ordinal))
            .ToArray();

        facts.Should().NotBeEmpty("the catalog read surface is mapped under " + CatalogPrefix);
        facts.Should().OnlyContain(
            fact => fact.Method == "GET",
            "the catalog page says nothing is edited there — a write route makes that a lie");
    }
}
