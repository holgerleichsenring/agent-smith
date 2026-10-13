using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-13-3890d: a tool's sandbox step whose host wait ran out comes back as a timed-out
/// result instead of an exception. Thrown, it left the calling model with an undetailed error and
/// ended a sub-agent after three of them; returned, every tool already renders a non-zero exit as
/// text the model can act on — retry, or change course. Cancellation still propagates.
/// </summary>
internal sealed class SandboxStepCall(ISandbox sandbox)
{
    public async Task<StepResult> RunAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        try
        {
            return await sandbox.RunStepAsync(step, progress, ct);
        }
        catch (TimeoutException ex)
        {
            return new StepResult(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: -1,
                TimedOut: true, DurationSeconds: 0, ErrorMessage: $"{ex.Message} — the sandbox did not answer in time; "
                + "retry the call, or continue without it", OutputContent: null);
        }
    }
}
