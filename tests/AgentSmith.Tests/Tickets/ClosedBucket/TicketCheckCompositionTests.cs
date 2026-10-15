using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Tickets.Checks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Tickets.ClosedBucket;

/// <summary>2026-10-15-114fc: the server composition serves the tracker observer with the closure rule it asks.</summary>
[Trait("TestProcess", "env-3")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class TicketCheckCompositionTests
{
    [Fact]
    public void TicketCheck_ServerComposition_ResolvesTheObserverWithTicketClosure()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        services.AddDashboardApi();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<TicketTrackerObserver>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<TicketBetweenAttemptsChecker>().Should().NotBeNull();
    }
}
