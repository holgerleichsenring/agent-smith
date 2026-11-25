using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>2026-11-25-9992b: where one repository of a workspace stood when it was saved — its branch and commit,
/// both on its remote, so a fresh clone lands on exactly that.</summary>
public sealed record WorkspaceRepoState(RepoConnection Repo, string Branch, string Head);
