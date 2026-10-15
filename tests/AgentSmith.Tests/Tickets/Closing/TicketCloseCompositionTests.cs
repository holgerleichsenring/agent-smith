using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets.Closing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Tickets.Closing;

/// <summary>2026-10-15-114fa: the server composition serves the close act, its host acts, the close-mark store and
/// the requirements-met start and wake that read it.</summary>
[Trait("TestProcess", "env-3")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class TicketCloseCompositionTests
{
    [Fact]
    public void TicketClose_ServerComposition_ResolvesTheActTheStoreAndTheEndpointServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        services.AddDashboardApi();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        provider.GetRequiredService<ITicketClosedMarkStore>().Should().BeOfType<ClosedTicketRepository>()
            .And.BeSameAs(provider.GetRequiredService<ITicketClosedMarkStore>());
        scope.ServiceProvider.GetRequiredService<TicketCloseAct>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TicketCloseHostActs>().Should().NotBeNull();
        provider.GetRequiredService<RequirementsMetStart>().Should().NotBeNull();
        provider.GetRequiredService<DependentsWake>().Should().NotBeNull();
    }
}
