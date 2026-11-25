using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: one toolchain container of the run's workspace, as a sandbox key reaches it — resolved at each
/// step, so it is the container of the workspace running now, and admitted through the workspace's step gate.
/// Disposing it removes nothing: the run's workspace owns its containers.
/// </summary>
public sealed class WorkspaceContainerSandbox(RunWorkspace workspace, string image) : ISandbox, ISandboxWrapper
{
    public string Image => image;

    public ISandbox Inner => workspace.Container(image);

    public string JobId => Inner.JobId;

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        using var admitted = await workspace.Gate.EnterAsync(cancellationToken);
        return await Inner.RunStepAsync(step, progress, cancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
