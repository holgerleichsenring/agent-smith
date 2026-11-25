namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992c: a host directory served at <c>/work/&lt;RepoName&gt;</c> inside an in-process
/// sandbox whose <c>/work</c> is a directory of its own — a run repository the host already cloned
/// (or the operator's <c>--source-path</c>). The sandbox never deletes it.
/// </summary>
public sealed record InProcessRepoMount(string RepoName, string SourceDir);
