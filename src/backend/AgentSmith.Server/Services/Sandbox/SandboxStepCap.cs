using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0200/p0407: the per-step wall-time cap, applied where it can be honoured.
/// <para>
/// The cap used to shape only the host's channel wait while the UNCLAMPED step went to
/// the agent — so a command allowed to run longer than the cap was killed by nobody: the
/// host gave up at cap + grace and threw, and the agent's own result (its captured output
/// and "timed out after Ns") arrived after the caller was gone. That is why a 978s build
/// surfaced as exit -1 with empty output. Clamping the step BEFORE it is pushed makes the
/// agent the single enforcer, and the host wait is only a backstop for a silent sandbox.
/// </para>
/// </summary>
public static class SandboxStepCap
{
    /// <summary>Grace on top of the cap before the host stops waiting for the agent's result.</summary>
    private const int ChannelGraceSeconds = 30;

    /// <summary>The step as the agent may run it — never longer than the operator's cap.</summary>
    public static Step Clamp(Step step, int capSeconds) =>
        step.TimeoutSeconds <= capSeconds ? step : step with { TimeoutSeconds = capSeconds };

    /// <summary>How long the host waits for a result before declaring the sandbox silent.</summary>
    public static TimeSpan ChannelWait(Step clampedStep) =>
        TimeSpan.FromSeconds(clampedStep.TimeoutSeconds + ChannelGraceSeconds);
}
