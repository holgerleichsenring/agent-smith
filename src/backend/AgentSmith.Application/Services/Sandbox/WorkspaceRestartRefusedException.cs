namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a repository's work could not be put on its remote, so the workspace was not replaced and stands
/// as it was. The caller answers with the reason; nothing was lost.
/// </summary>
public sealed class WorkspaceRestartRefusedException(string reason)
    : InvalidOperationException($"the workspace was not restarted, because {reason}");
