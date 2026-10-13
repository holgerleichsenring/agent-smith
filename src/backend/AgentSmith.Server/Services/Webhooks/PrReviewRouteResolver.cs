using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// p0167a: resolves which (project, repo) a pull-request event belongs to: the event's repo URL must
/// match a configured project repo (unconfigured repos never trigger). 2026-10-13-0b82a: no open or
/// push starts a review any more, so the PR-label override of the pipeline went with them; the label
/// handlers use the match alone.
/// </summary>
public sealed class PrReviewRouteResolver(IConfiguredRepoFinder repoFinder)
{
    public const string DefaultPipeline = "pr-review";

    public PrReviewRoute? Resolve(AgentSmithConfig config, string repoUrl) =>
        repoFinder.Find(config, repoUrl) is { } match
            ? new PrReviewRoute(match.ProjectName, match.Repo.Name, DefaultPipeline)
            : null;
}
