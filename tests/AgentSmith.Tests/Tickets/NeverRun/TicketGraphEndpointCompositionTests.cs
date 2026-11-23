using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Tickets.NeverRun;

/// <summary>2026-10-14-dbcen: the server composition resolves what the requires route (2026-11-23-849e: the graph route is gone) and the never-run
/// rows are handed.</summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class TicketGraphEndpointCompositionTests
{
    [Fact]
    public void TicketGraphEndpoints_ResolveFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        services.AddDashboardApi();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TicketRequiresReader>().Should().NotBeNull();
        sp.GetRequiredService<FiledTicketRows>().Should().NotBeNull();
        sp.GetRequiredService<TicketListReader>().Should().NotBeNull();
        sp.GetRequiredService<TicketDetailReader>().Should().NotBeNull();
    }
}
