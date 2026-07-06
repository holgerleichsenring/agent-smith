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
        resolver.Setup(r => r.Resolve(It.IsAny<ResolvedProject>(), It.IsAny<ContextYamlStackResources?>()))
            .Returns(ResourceLimits.Default);
        return resolver.Object;
    }

    public static ISandboxCapacityProbe AlwaysAdmit()
    {
        var probe = new Mock<ISandboxCapacityProbe>();
        probe.Setup(p => p.HasCapacityAsync(It.IsAny<ResourceLimits>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CapacityDecision.Admit());
        return probe.Object;
    }
}
