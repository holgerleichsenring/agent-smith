using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Pipeline;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

public sealed class StepDataFlowResolverTests
{
    [Fact]
    public void Resolve_KnownPreset_ReturnsRegisteredFlow()
    {
        var flow = new TestFlow("code");
        var resolver = new StepDataFlowResolver(new[] { flow });

        resolver.Resolve("code").Should().BeSameAs(flow);
    }

    [Fact]
    public void Resolve_UnknownPreset_ReturnsNull()
    {
        var resolver = new StepDataFlowResolver(Array.Empty<IStepDataFlow>());

        resolver.Resolve("unknown").Should().BeNull();
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var flow = new TestFlow("Code");
        var resolver = new StepDataFlowResolver(new[] { flow });

        resolver.Resolve("code").Should().BeSameAs(flow);
    }

    private sealed class TestFlow(string name) : IStepDataFlow
    {
        public string PresetName => name;
        public IReadOnlyList<StepDataFlowEdge> Edges => Array.Empty<StepDataFlowEdge>();
    }
}
