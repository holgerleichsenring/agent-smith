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

    // p0336: a footprint calculator that returns a fixed small footprint — the
    // spawn tests exercise admission control, not the sizing itself (covered by
    // RunFootprintCalculatorTests).
    public static IRunFootprintCalculator StubCalculator()
    {
        var calc = new Mock<IRunFootprintCalculator>();
        calc.Setup(c => c.CalculateAsync(
                It.IsAny<ResolvedProject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunFootprintBreakdown(
                [], "1", "4Gi", 1_000_000_000, 4L * 1024 * 1024 * 1024, [], "stub footprint"));
        return calc.Object;
    }

    // p0336: a budget that admits every reservation — the admit path, mirroring
    // the old AlwaysAdmit probe double.
    public static ICapacityBudget AlwaysReserve()
    {
        var budget = new Mock<ICapacityBudget>();
        budget.Setup(b => b.RecordAsync(
                It.IsAny<string>(), It.IsAny<RunFootprintBreakdown>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        budget.Setup(b => b.TryReserveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        budget.Setup(b => b.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return budget.Object;
    }
}
