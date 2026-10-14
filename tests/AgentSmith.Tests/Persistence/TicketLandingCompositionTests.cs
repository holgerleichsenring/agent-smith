using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Sweep;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence;

/// <summary>2026-10-14-dbceg: the server composition serves the landing store, the recorder the refresh
/// holds, and the re-read as a hosted service with its own health.</summary>
[Trait("TestProcess", "env-2")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class TicketLandingCompositionTests
{
    [Fact]
    public void TicketLanding_ServerComposition_ResolvesStoreRecorderRefreshAndReReadService()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITicketLandingStore>().Should().BeOfType<LandedTicketRepository>()
            .And.BeSameAs(provider.GetRequiredService<ITicketLandingStore>());
        provider.GetRequiredService<TicketLandingRecorder>().Should().NotBeNull();
        provider.GetRequiredService<PrReviewRecordRefresh>().Should().NotBeNull();
        var reread = provider.GetRequiredService<PrRecordReReadHostedService>();
        provider.GetServices<IHostedService>().Should().Contain(reread);
        provider.GetServices<ISubsystemHealth>().Select(h => h.Name).Should().Contain("pr-record-reread");
    }
}
