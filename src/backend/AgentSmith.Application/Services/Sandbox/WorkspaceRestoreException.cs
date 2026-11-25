namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: the run's workspace was replaced and a repository could not be put back where it stood. The run
/// cannot continue on a tree it does not have, so this ends the run rather than becoming a tool's answer.
/// </summary>
public sealed class WorkspaceRestoreException(string message) : Exception(message);
