using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: one manifest change to deliver — where, on which branch, with which pull request text, and
/// whether a person approves it or agent-smith merges it in <paramref name="Protection"/>, and how its CI is read back.</summary>
public sealed record GitOpsOrder(
    RepoConnection Repo, string Path, string Content, BranchName Branch, string Title, string Body, bool Approval,
    BranchProtection Protection,
    CiReadRequest Ci);
