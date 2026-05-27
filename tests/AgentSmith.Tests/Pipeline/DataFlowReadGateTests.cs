using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Pipeline;

public sealed class DataFlowReadGateTests
{
    private const string Pipeline = "test";

    private static IPhaseDataFlow Flow(params PhaseDataFlowEdge[] edges)
        => new TestFlow(Pipeline, edges);

    private static DataFlowReadGate Build(IPhaseDataFlow flow, bool enforce)
        => new(
            new SingleFlowResolver(flow),
            Options.Create(new PipelineDataFlowConfig { Enforce = enforce }),
            NullLogger<DataFlowReadGate>.Instance);

    private static (DataFlowReadGate Gate, PipelineContext Context) Attach(
        IPhaseDataFlow flow, bool enforce, string activeStep)
    {
        var gate = Build(flow, enforce);
        var context = new PipelineContext();
        var handle = gate.AttachToStep(activeStep, Pipeline, context);
        handle.Should().NotBeNull("the test flow resolver returns a non-null flow");
        return (gate, context);
    }

    [Fact]
    public void OnRead_DeclaredKey_DoesNotThrow()
    {
        var (_, context) = Attach(
            Flow(new PhaseDataFlowEdge("Producer", "Consumer", new[] { "Plan" })),
            enforce: true, "Consumer");
        context.Set("Plan", "value");

        var act = () => context.Get<string>("Plan");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_UndeclaredKey_EnforceTrue_Throws()
    {
        var (_, context) = Attach(
            Flow(new PhaseDataFlowEdge("Producer", "Consumer", new[] { "Plan" })),
            enforce: true, "Consumer");
        context.Set("Diff", "value");

        var act = () => context.Get<string>("Diff");

        act.Should().Throw<DataFlowViolationException>()
            .Which.OffendingKey.Should().Be("Diff");
    }

    [Fact]
    public void OnRead_UndeclaredKey_EnforceFalse_DoesNotThrow()
    {
        var (_, context) = Attach(
            Flow(new PhaseDataFlowEdge("Producer", "Consumer", new[] { "Plan" })),
            enforce: false, "Consumer");
        context.Set("Diff", "value");

        var act = () => context.Get<string>("Diff");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_WildcardFromStep_AllowsAnyProducer()
    {
        var (_, context) = Attach(
            Flow(new PhaseDataFlowEdge("*", "Consumer", new[] { "Plan" })),
            enforce: true, "Consumer");
        context.Set("Plan", "value");

        var act = () => context.Get<string>("Plan");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_WildcardToStep_AllowsAnyConsumer()
    {
        var (_, context) = Attach(
            Flow(new PhaseDataFlowEdge("Producer", "*", new[] { "Plan" })),
            enforce: true, "AnyStep");
        context.Set("Plan", "value");

        var act = () => context.Get<string>("Plan");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_WildcardKey_AllowsAnyKey()
    {
        var (_, context) = Attach(
            Flow(new PhaseDataFlowEdge("*", "*", new[] { "*" })),
            enforce: true, "Anything");
        context.Set("AnyKeyAtAll", "value");

        var act = () => context.Get<string>("AnyKeyAtAll");

        act.Should().NotThrow();
    }

    [Fact]
    public void OnRead_EmptyEdges_EnforceTrue_AlwaysThrows()
    {
        var (_, context) = Attach(Flow(), enforce: true, "Consumer");
        context.Set("Plan", "value");

        var act = () => context.Get<string>("Plan");

        act.Should().Throw<DataFlowViolationException>();
    }

    [Fact]
    public void OnRead_DataFlowViolationException_NamesActiveStepAndKey()
    {
        var (_, context) = Attach(Flow(), enforce: true, "Consumer");
        context.Set("Diff", "value");

        DataFlowViolationException? captured = null;
        try { context.Get<string>("Diff"); } catch (DataFlowViolationException ex) { captured = ex; }

        captured.Should().NotBeNull();
        captured!.ActivePhaseStep.Should().Be("Consumer");
        captured.OffendingKey.Should().Be("Diff");
    }

    [Fact]
    public void AttachToStep_ResolverReturnsNull_NoOpAndContextStaysUnguarded()
    {
        var gate = new DataFlowReadGate(
            new NullResolver(),
            Options.Create(new PipelineDataFlowConfig { Enforce = true }),
            NullLogger<DataFlowReadGate>.Instance);
        var context = new PipelineContext();

        var handle = gate.AttachToStep("AnyStep", Pipeline, context);

        handle.Should().BeNull();
    }

    private sealed class TestFlow(string name, IReadOnlyList<PhaseDataFlowEdge> edges) : IPhaseDataFlow
    {
        public string PresetName => name;
        public IReadOnlyList<PhaseDataFlowEdge> Edges => edges;
    }

    private sealed class SingleFlowResolver(IPhaseDataFlow flow) : IPhaseDataFlowResolver
    {
        public IPhaseDataFlow? Resolve(string pipelineName) => flow;
    }

    private sealed class NullResolver : IPhaseDataFlowResolver
    {
        public IPhaseDataFlow? Resolve(string pipelineName) => null;
    }
}
