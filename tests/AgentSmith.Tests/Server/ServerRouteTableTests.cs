using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0506: run control — cancel, answer and retry — was mapped unconditionally, next to
/// the health and webhook routes a degraded server reports itself through. With
/// AGENTSMITH_UI_API_ENABLED=false cancel already 500s (its JobsBroadcaster is registered
/// only inside AddDashboardApi), but answer and retry resolved fine from the
/// unconditional chain: an unauthenticated caller could publish an answer to a master
/// blocked on a question, and move a parked ticket back into a trigger status. They now
/// ride the dashboard gate — a holding position until p0503a gives them a permission.
/// </summary>
public sealed class ServerRouteTableTests
{
    private static readonly string[] RunControlRoutes =
    [
        "/api/runs/{runId}/cancel", "/api/runs/{runId}/answer", "/api/runs/{runId}/retry",
    ];

    [Fact]
    public void Endpoints_MapServerEndpoints_DoesNotMapRunControl()
    {
        var patterns = ServerRouteTable.Patterns(app => app.MapServerEndpoints());

        patterns.Should().Contain("/health", "the diagnostic surface stays unconditional");
        patterns.Should().Contain("/webhook/github");
        patterns.Should().NotIntersectWith(RunControlRoutes);
    }

    [Fact]
    public void Endpoints_MapDashboardApi_MapsCancelAnswerAndRetry()
    {
        var patterns = ServerRouteTable.Patterns(app => app.MapDashboardApi());

        patterns.Should().Contain(RunControlRoutes);
    }

    // The seam's whole claim: registration is enough, nothing is INSTANTIATED. Proven by
    // poisoning the configuration loader — /api/runs/{runId}/retry takes an
    // AgentSmithConfig, which is registered as "ask the loader", so an enumeration that
    // resolved its handler's parameters would throw here. It also means no RunRepository
    // (a database) and no Redis multiplexer is built to answer "is this route mapped?".
    [Fact]
    public void Endpoints_RouteEnumeration_ResolvesNoService()
    {
        var patterns = ServerRouteTable.Patterns(
            app => app.MapServerEndpoints().MapDashboardApi(),
            services =>
            {
                services.RemoveAll<IConfigurationLoader>();
                services.AddSingleton<IConfigurationLoader, ExplodingConfigurationLoader>();
            });

        patterns.Should().Contain("/api/runs/{runId}/retry");
    }

    private sealed class ExplodingConfigurationLoader : IConfigurationLoader
    {
        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath) =>
            throw new InvalidOperationException("the route table resolved a service");
    }
}
