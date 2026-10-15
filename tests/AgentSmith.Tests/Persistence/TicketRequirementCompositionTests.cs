using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence;

/// <summary>2026-10-14-dbcef: the server composition serves the ticket requirements from the relational
/// store, as one singleton the claim service and the wake can hold.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class TicketRequirementCompositionTests
{
    [Fact]
    public void TicketRequirementStore_ServerComposition_ResolvesTheRelationalSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<ITicketRequirementStore>();

        store.Should().BeOfType<TicketRequirementRepository>();
        provider.GetRequiredService<ITicketRequirementStore>().Should().BeSameAs(store);
    }
}
