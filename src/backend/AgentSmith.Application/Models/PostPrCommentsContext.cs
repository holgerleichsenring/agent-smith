using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for posting the compiled pr-review comments on the PR under
/// review. Repo is the PR's repo (Repos[0] — the pr-event webhook scopes the
/// run via SourceOverrideRepo); Review is the compiled summary + inline set.
/// </summary>
public sealed record PostPrCommentsContext(
    RepoConnection Repo,
    string PrNumber,
    PrReviewSummary Review,
    PipelineContext Pipeline) : ICommandContext;
