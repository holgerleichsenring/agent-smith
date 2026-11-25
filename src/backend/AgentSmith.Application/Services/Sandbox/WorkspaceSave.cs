namespace AgentSmith.Application.Services.Sandbox;

/// <summary>2026-11-25-9992b: every repository's saved state, or why one could not be saved — then nothing may replace
/// the workspace, because its work exists only there.</summary>
public sealed record WorkspaceSave(IReadOnlyList<WorkspaceRepoState> Repos, string? Refusal)
{
    public static WorkspaceSave Saved(IReadOnlyList<WorkspaceRepoState> repos) => new(repos, null);

    public static WorkspaceSave Refused(string reason) => new([], reason);
}
