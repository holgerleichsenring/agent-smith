using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pipeline;

public sealed class DataFlowReadGateTests
{
    private static IPhaseDataFlow Flow(params PhaseDataFlowEdge[] edges)
        => new TestFlow("test", edges);

    [Fact]
    public void OnRead_DeclaredKey_DoesNotThrow()
    {
        var flow = Flow(new PhaseDataFlowEdge("Producer", "Consumer", new[] { "Plan" }));
        var gate = new DataFlowReadGate("Consumer", flow, enforce: true, NullLogger.Instance);

        var act = () => gate.OnRead("Plan");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_UndeclaredKey_EnforceTrue_Throws()
    {
        var flow = Flow(new PhaseDataFlowEdge("Producer", "Consumer", new[] { "Plan" }));
        var gate = new DataFlowReadGate("Consumer", flow, enforce: true, NullLogger.Instance);

        var act = () => gate.OnRead("Diff");

        act.Should().Throw<DataFlowViolationException>()
            .Which.OffendingKey.Should().Be("Diff");
    }

    [Fact]
    public void OnRead_UndeclaredKey_EnforceFalse_DoesNotThrow()
    {
        var flow = Flow(new PhaseDataFlowEdge("Producer", "Consumer", new[] { "Plan" }));
        var gate = new DataFlowReadGate("Consumer", flow, enforce: false, NullLogger.Instance);

        var act = () => gate.OnRead("Diff");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_WildcardFromStep_AllowsAnyProducer()
    {
        var flow = Flow(new PhaseDataFlowEdge("*", "Consumer", new[] { "Plan" }));
        var gate = new DataFlowReadGate("Consumer", flow, enforce: true, NullLogger.Instance);

        var act = () => gate.OnRead("Plan");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_WildcardToStep_AllowsAnyConsumer()
    {
        var flow = Flow(new PhaseDataFlowEdge("Producer", "*", new[] { "Plan" }));
        var gate = new DataFlowReadGate("AnyStep", flow, enforce: true, NullLogger.Instance);

        var act = () => gate.OnRead("Plan");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_WildcardKey_AllowsAnyKey()
    {
        var flow = Flow(new PhaseDataFlowEdge("*", "*", new[] { "*" }));
        var gate = new DataFlowReadGate("Anything", flow, enforce: true, NullLogger.Instance);

        var act = () => gate.OnRead("AnyKeyAtAll");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_EmptyEdges_EnforceTrue_AlwaysThrows()
    {
        var gate = new DataFlowReadGate("Consumer", Flow(), enforce: true, NullLogger.Instance);

        var act = () => gate.OnRead("Plan");

        act.Should().Throw<DataFlowViolationException>();
    }

    [Fact]
    public void OnRead_DataFlowViolationException_NamesActiveStepAndKey()
    {
        var gate = new DataFlowReadGate("Consumer", Flow(), enforce: true, NullLogger.Instance);

        DataFlowViolationException? captured = null;
        try { gate.OnRead("Diff"); } catch (DataFlowViolationException ex) { captured = ex; }

        captured.Should().NotBeNull();
        captured!.ActivePhaseStep.Should().Be("Consumer");
        captured.OffendingKey.Should().Be("Diff");
    }

    private sealed class TestFlow(string name, IReadOnlyList<PhaseDataFlowEdge> edges) : IPhaseDataFlow
    {
        public string PresetName => name;
        public IReadOnlyList<PhaseDataFlowEdge> Edges => edges;
    }
}
