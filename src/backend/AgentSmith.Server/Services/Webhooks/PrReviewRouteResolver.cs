using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// p0167a: resolves which (project, repo, pipeline) a pr-opened / pr-synchronize
/// event routes to. Shared by the three platform PrEvent webhook handlers so the
/// routing rule lives in one place: the event's repo URL must match a configured
/// project repo (unconfigured repos never trigger), the pipeline defaults to
/// pr-review, and the matched project's platform trigger may override the route
/// per PR via pipeline_from_label (the operator's opt-out lever — a mapped PR
/// label wins over the default, keys checked in config order per p0072).
/// </summary>
public sealed class PrReviewRouteResolver
{
    public const string DefaultPipeline = "pr-review";

    public PrReviewRoute? Resolve(
        AgentSmithConfig config, string platformKind, string repoUrl,
        IReadOnlyList<string> prLabels)
    {
        foreach (var (projectName, project) in config.Projects)
        {
            var repo = FindMatchingRepo(project, repoUrl);
            if (repo is null) continue;

            var trigger = TriggerSelectionHelper.ByKind(project, platformKind);
            var pipeline = ResolveLabelOverride(trigger, prLabels) ?? DefaultPipeline;
            return new PrReviewRoute(projectName, repo.Name, pipeline);
        }
        return null;
    }

    private static RepoConnection? FindMatchingRepo(ResolvedProject project, string repoUrl)
    {
        var candidate = NormalizeRepoUrl(repoUrl);
        foreach (var repo in project.Repos)
            if (repo.Url is not null && candidate.Contains(NormalizeRepoUrl(repo.Url), StringComparison.Ordinal))
                return repo;
        return null;
    }

    private static string? ResolveLabelOverride(
        WebhookTriggerConfig? trigger, IReadOnlyList<string> prLabels)
    {
        if (trigger?.PipelineFromLabel is not { Count: > 0 } map) return null;
        foreach (var (label, pipeline) in map)
            if (prLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
                return pipeline;
        return null;
    }

    /// <summary>Host + path, lowercased, no scheme/userinfo/.git suffix — so a
    /// payload clone_url ("https://user@host/org/repo.git") matches the
    /// operator's configured web URL ("https://host/org/repo").</summary>
    private static string NormalizeRepoUrl(string url)
    {
        var normalized = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? $"{uri.Host}{uri.AbsolutePath}"
            : url;
        normalized = normalized.TrimEnd('/');
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];
        return normalized.ToLowerInvariant();
    }
}
