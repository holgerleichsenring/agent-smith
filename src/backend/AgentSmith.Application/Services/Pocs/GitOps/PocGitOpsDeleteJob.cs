using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81ed: one delete commit of a PoC — the run, the row, the source repo, the deploy folder, the
/// pocs.gitops bounds and who asked.</summary>
public sealed record PocGitOpsDeleteJob(
    PipelineContext Pipeline, PocGitOpsSubject Poc, RepoConnection Repo, string Folder, PocGitOpsConfig Bounds, string? Actor);
