using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0167a: context for fetching + parsing the diff of the PR under review.
/// Repo is the PR's repo (the pr-event webhook scopes the run to it via
/// SourceOverrideRepo, so Repos[0] is that repo); PrNumber is the platform
/// PR/MR identifier seeded by the webhook.
/// </summary>
public sealed record AnalyzePrDiffContext(
    RepoConnection Repo,
    string PrNumber,
    PipelineContext Pipeline) : ICommandContext;
