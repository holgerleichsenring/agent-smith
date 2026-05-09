using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Pipeline;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

public sealed class PhaseDataFlowResolverTests
{
    [Fact]
    public void Resolve_KnownPreset_ReturnsRegisteredFlow()
    {
        var flow = new TestFlow("fix-bug");
        var resolver = new PhaseDataFlowResolver(new[] { flow });

        resolver.Resolve("fix-bug").Should().BeSameAs(flow);
    }

    [Fact]
    public void Resolve_UnknownPreset_ReturnsNull()
    {
        var resolver = new PhaseDataFlowResolver(Array.Empty<IPhaseDataFlow>());

        resolver.Resolve("unknown").Should().BeNull();
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var flow = new TestFlow("Fix-Bug");
        var resolver = new PhaseDataFlowResolver(new[] { flow });

        resolver.Resolve("fix-bug").Should().BeSameAs(flow);
    }

    private sealed class TestFlow(string name) : IPhaseDataFlow
    {
        public string PresetName => name;
        public IReadOnlyList<PhaseDataFlowEdge> Edges => Array.Empty<PhaseDataFlowEdge>();
    }
}
