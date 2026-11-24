using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-23-c81ea: one direct commit to a GitOps repository's default branch — the files it writes and
/// deletes and its message. It names no branch: <see cref="GitOpsDelivery.CommitAsync"/> reads the default branch, so
/// no stray branch can be created.</summary>
public sealed record GitOpsCommitOrder(RepoConnection Repo, IReadOnlyList<RepoFile> Files, string Message);
