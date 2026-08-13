using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0407: the agent must be bound by the same cap the host waits for. Before this,
/// a command allowed to outlive the cap was killed by nobody — the host threw at
/// cap + grace and the agent's timed-out result (with its output) came too late,
/// which is how a 978s build surfaced as exit -1 with nothing.
/// </summary>
public sealed class SandboxStepCapTests
{
    [Fact]
    public void Clamp_StepAboveCap_IsLoweredToCap()
    {
        var step = Run(1800);

        SandboxStepCap.Clamp(step, 900).TimeoutSeconds.Should().Be(900);
    }

    [Fact]
    public void Clamp_StepBelowCap_IsUnchanged()
    {
        var step = Run(120);

        SandboxStepCap.Clamp(step, 900).Should().BeSameAs(step);
    }

    [Fact]
    public void Clamp_KeepsEverythingElseAboutTheStep()
    {
        var clamped = SandboxStepCap.Clamp(Run(1800), 900);

        clamped.Command.Should().Be("/bin/sh");
        clamped.Args.Should().BeEquivalentTo("-c", "dotnet build");
        clamped.Kind.Should().Be(StepKind.Run);
    }

    [Fact]
    public void ChannelWait_IsTheCappedStepPlusGrace_SoTheAgentAnswersFirst()
    {
        var clamped = SandboxStepCap.Clamp(Run(1800), 900);

        SandboxStepCap.ChannelWait(clamped).Should().Be(TimeSpan.FromSeconds(930));
    }

    private static Step Run(int timeoutSeconds) => new(
        Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
        Command: "/bin/sh", Args: ["-c", "dotnet build"], TimeoutSeconds: timeoutSeconds);
}
