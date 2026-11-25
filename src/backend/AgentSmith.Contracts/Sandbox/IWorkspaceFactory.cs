namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-11-25-9992b: creates a run's workspace. It replaces one sandbox per repository and image: the repositories
/// of a run lie side by side under one /work, and every toolchain container sees all of them.
/// </summary>
public interface IWorkspaceFactory
{
    Task<IWorkspace> CreateAsync(WorkspaceSpec spec, CancellationToken cancellationToken);
}
