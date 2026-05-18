using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// p0140a: turns an incoming ticket envelope (labels + area-path + source-repo-url +
/// to-address) into the list of (project, pipeline) tuples that match it.
/// Stateless; pure function over config + envelope.
///
/// Ambiguous resolution is intentional: a ticket that matches more than one project is
/// returned with all matches. p0140b's webhook handlers will spawn a pipeline run for
/// each. Zero matches return an empty list — the handler decides whether to log,
/// comment, or just drop.
///
/// Within a matched project, pipeline selection delegates to <see cref="PipelineResolver"/>
/// so the existing pipeline-from-label / default-pipeline / global-fallback chain stays
/// authoritative. The resolver itself is project-only.
/// </summary>
public sealed class ProjectResolver(ILogger<ProjectResolver>? logger = null) : IEnvelopeProjectResolver
{
    public IReadOnlyList<ProjectMatch> Resolve(AgentSmithConfig config, IncomingTicketEnvelope envelope)
    {
        var matches = new List<ProjectMatch>();
        foreach (var (projectName, project) in config.Projects)
        {
            foreach (var (kind, trigger) in EnumerateTriggers(project))
            {
                if (!Matches(trigger, project, envelope)) continue;

                var pipeline = PipelineResolver.Resolve(
                    trigger, envelope.Labels, config.PipelineTriggers, logger as ILogger)
                    ?? trigger.DefaultPipeline;

                if (string.IsNullOrEmpty(pipeline))
                {
                    logger?.LogWarning(
                        "ProjectResolver: project '{Project}' matched envelope on {Kind} but pipeline resolution returned null/empty; skipping.",
                        projectName, kind);
                    continue;
                }

                matches.Add(new ProjectMatch(projectName, pipeline, kind));
            }
        }
        return matches;
    }

    private static IEnumerable<(string Kind, WebhookTriggerConfig Trigger)> EnumerateTriggers(ResolvedProject project)
    {
        if (project.GithubTrigger is not null) yield return ("github", project.GithubTrigger);
        if (project.GitlabTrigger is not null) yield return ("gitlab", project.GitlabTrigger);
        if (project.AzuredevopsTrigger is not null) yield return ("azuredevops", project.AzuredevopsTrigger);
        if (project.JiraTrigger is not null) yield return ("jira", project.JiraTrigger);
    }

    private static bool Matches(WebhookTriggerConfig trigger, ResolvedProject project, IncomingTicketEnvelope envelope)
    {
        var resolution = trigger.ProjectResolution;
        if (resolution is null) return false;

        return resolution.Strategy switch
        {
            ResolutionStrategy.Tag       => MatchesTag(envelope, resolution.Value),
            ResolutionStrategy.AreaPath  => AreaPathNormalizer.IsPrefix(resolution.Value, envelope.AreaPath),
            ResolutionStrategy.Repo      => MatchesRepo(envelope, project),
            ResolutionStrategy.ToAddress => MatchesToAddress(envelope, resolution.Value),
            _ => false,
        };
    }

    private static bool MatchesTag(IncomingTicketEnvelope envelope, string value)
        => envelope.Labels.Any(l => string.Equals(l, value, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesRepo(IncomingTicketEnvelope envelope, ResolvedProject project)
    {
        if (string.IsNullOrEmpty(envelope.SourceRepoUrl)) return false;
        if (project.Repos.Count != 1) return false;
        var url = project.Repos[0].Url;
        return !string.IsNullOrEmpty(url)
            && string.Equals(url, envelope.SourceRepoUrl, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesToAddress(IncomingTicketEnvelope envelope, string value)
        => !string.IsNullOrEmpty(envelope.ToAddress)
            && string.Equals(envelope.ToAddress, value, StringComparison.OrdinalIgnoreCase);
}

