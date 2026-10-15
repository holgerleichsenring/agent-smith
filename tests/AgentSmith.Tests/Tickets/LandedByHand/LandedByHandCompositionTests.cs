using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Tickets.LandedByHand;

/// <summary>2026-10-14-dbcei: the server composition resolves what the landed-by-hand route, the standings and the
/// conversation's start are handed.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class LandedByHandCompositionTests
{
    [Fact]
    public void LandedByHand_ServerComposition_ResolvesTheRecorderAndTheEndpointServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        services.AddDashboardApi();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TicketLandingRecorder>().Should().NotBeNull();
        sp.GetRequiredService<TicketProjects>().Should().NotBeNull();
        sp.GetRequiredService<ITicketLandingStore>().Should().NotBeNull();
        sp.GetRequiredService<ITicketProviderFactory>().Should().NotBeNull();
        sp.GetRequiredService<TicketStandings>().Should().NotBeNull();
        sp.GetRequiredService<UnmetTicketRequirements>().Should().NotBeNull("the conversation's start asks it in its own scope");
    }
}
