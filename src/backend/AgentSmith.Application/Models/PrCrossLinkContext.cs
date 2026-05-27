using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the PrCrossLinkCommand — pass-2 of the cross-linking flow.
/// Reads OpenedPullRequests + OpenedPullRequestBodies from the pipeline and
/// updates each opened PR's body via the per-repo ISourceProvider.
/// </summary>
public sealed record PrCrossLinkContext(
    IReadOnlyList<RepoConnection> Configs,
    PipelineContext Pipeline) : ICommandContext;
