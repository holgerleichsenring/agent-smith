using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

public sealed class PipelineContextReadGateTests
{
    [Fact]
    public void Get_WithGateAttached_InvokesGate()
    {
        var context = new PipelineContext();
        context.Set("Plan", "value");
        var gate = new RecordingGate();

        using (context.AttachReadGate(gate))
            context.Get<string>("Plan");

        gate.Reads.Should().ContainSingle().Which.Should().Be("Plan");
    }

    [Fact]
    public void TryGet_WithGateAttached_InvokesGate()
    {
        var context = new PipelineContext();
        var gate = new RecordingGate();

        using (context.AttachReadGate(gate))
            context.TryGet<string>("Missing", out _);

        gate.Reads.Should().ContainSingle().Which.Should().Be("Missing");
    }

    [Fact]
    public void Set_DoesNotInvokeGate()
    {
        var context = new PipelineContext();
        var gate = new RecordingGate();

        using (context.AttachReadGate(gate))
            context.Set("Key", "value");

        gate.Reads.Should().BeEmpty();
    }

    [Fact]
    public void GateScope_DisposeDetaches()
    {
        var context = new PipelineContext();
        context.Set("Plan", "value");
        var gate = new RecordingGate();

        using (context.AttachReadGate(gate)) { /* in scope */ }
        context.Get<string>("Plan");

        gate.Reads.Should().BeEmpty();
    }

    [Fact]
    public void AttachReadGate_NestedAttach_Throws()
    {
        var context = new PipelineContext();
        using var first = context.AttachReadGate(new RecordingGate());

        var act = () => context.AttachReadGate(new RecordingGate());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GateScope_RecreateAfterDispose_Works()
    {
        var context = new PipelineContext();
        using (context.AttachReadGate(new RecordingGate())) { }
        var act = () => context.AttachReadGate(new RecordingGate());

        act.Should().NotThrow();
    }

    private sealed class RecordingGate : IPipelineContextReadGate
    {
        public List<string> Reads { get; } = new();
        public void OnRead(string key) => Reads.Add(key);
    }
}
