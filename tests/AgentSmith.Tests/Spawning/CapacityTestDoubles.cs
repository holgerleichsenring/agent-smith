using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Moq;

namespace AgentSmith.Tests.Spawning;

/// <summary>
/// p0269a: shared test doubles for the capacity-admission dependencies of
/// SpawnPipelineRunsUseCase, so pre-existing spawn tests keep exercising the
/// admit path without each re-declaring the mocks.
/// </summary>
internal static class CapacityTestDoubles
{
    public static ISandboxResourceResolver PassthroughResolver()
    {
        var resolver = new Mock<ISandboxResourceResolver>();
        resolver.Setup(r => r.Resolve(
                It.IsAny<ResolvedProject>(), It.IsAny<string?>(), It.IsAny<ContextYamlStackResources?>()))
            .Returns(ResourceLimits.Default);
        return resolver.Object;
    }

    // p0320b: in-process default — no orchestrator pod, footprint carries null.
    public static IOrchestratorResourceResolver NoOrchestrator() =>
        new NullOrchestratorResourceResolver();

    public static ISandboxCapacityProbe AlwaysAdmit()
    {
        var probe = new Mock<ISandboxCapacityProbe>();
        probe.Setup(p => p.HasCapacityAsync(It.IsAny<RunFootprint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CapacityDecision.Admit());
        return probe.Object;
    }

    // p0320c: the DB-free queue — always empty, records nothing. Pre-existing
    // spawn tests exercise the direct-claim path with it.
    public static AgentSmith.Contracts.Services.ICapacityQueue EmptyQueue() =>
        new AgentSmith.Application.Services.Spawning.NoOpCapacityQueue();
}
