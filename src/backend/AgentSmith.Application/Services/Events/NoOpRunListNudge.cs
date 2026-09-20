using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// 2026-09-20-9f00: the binding for a composition that has no hub to nudge — the CLI, the
/// harness, and a server started with the UI API switched off. Announcing a run row to
/// nobody is exactly nothing, so this records nothing and never fails.
/// </summary>
public sealed class NoOpRunListNudge : IRunListNudge
{
    public Task RunsChangedAsync(string runId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
