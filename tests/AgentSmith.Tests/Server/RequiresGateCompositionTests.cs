using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Sweep;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server;

/// <summary>2026-10-14-dbceh: the server composition gives the claim service, the funnel, the landing recorder,
/// the rework handler and the re-read the requirement read, the wake and the start they are built with.</summary>
[Trait("TestProcess", "env-3")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class RequiresGateCompositionTests
{
    [Fact]
    public void RequiresGate_ServerComposition_ResolvesClaimServiceFunnelRecorderHandlerAndReRead()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        provider.GetRequiredService<UnmetTicketRequirements>().Should().BeSameAs(provider.GetRequiredService<UnmetTicketRequirements>());
        provider.GetRequiredService<ITicketClaimService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ISpawnPipelineRunsUseCase>().Should().NotBeNull();
        provider.GetRequiredService<TicketLandingRecorder>().Should().NotBeNull();
        provider.GetRequiredService<ReworkNudgeHandler>().Should().NotBeNull();
        provider.GetRequiredService<DependentsWake>().Should().BeSameAs(provider.GetRequiredService<DependentsWake>());
        provider.GetRequiredService<RequirementsMetStart>().Should().NotBeNull();
        provider.GetRequiredService<PrRecordReReadHostedService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.SpecDialog.Continuation.OperatorStart>().Should().NotBeNull();
    }
}
