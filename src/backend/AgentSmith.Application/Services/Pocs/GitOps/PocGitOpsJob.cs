using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: one GitOps commit of a PoC — the run, the row, the desired state (and whether an act set
/// it), the render input, the source repo and the deploy folder, and the pocs.gitops bounds.</summary>
public sealed record PocGitOpsJob(
    PipelineContext Pipeline, PocGitOpsSubject Poc, PocDesiredState Desired, bool FromAct, PocRenderInput Input,
    RepoConnection Repo, string Folder, PocGitOpsConfig Bounds, string? Actor);
