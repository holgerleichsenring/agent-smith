using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs.Rollouts;

/// <summary>
/// 2026-11-23-c81ec: one run of a PoC for its Rollout page. The route already required runs.read in the PoC's project.
/// The URL is given only once the revision rolled out healthy and the PoC is ready; the GitOps commit's link is
/// &lt;source repo web URL&gt;/commit/&lt;sha&gt; (GitHub, Azure DevOps and GitLab all answer it) when the repo has an
/// http(s) URL.
/// </summary>
internal sealed class PocRolloutReader(PocRepository pocs, PocRolloutRepository rollouts, TicketProjects projects, PocSourceRepos repos)
{
    public async Task<PocRolloutAnswer?> ReadAsync(long pocId, string runId, CancellationToken ct)
    {
        if (await pocs.FindAsync(pocId, ct) is not { } poc) return null;
        var revision = await rollouts.RevisionOfRunAsync(poc, runId, ct);
        var config = projects.Config();
        var target = config.Pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, poc.Target, StringComparison.OrdinalIgnoreCase));
        var healthy = revision?.RolloutState == PocRolloutStates.Healthy && poc.State == PocStates.Ready;
        var host = target?.DomainPattern?.Replace("{app}", poc.Name, StringComparison.Ordinal);
        var repoUrl = target is null ? null : repos.Resolve(config, target).Repo?.Url;
        return new PocRolloutAnswer(poc.Id, poc.Name, runId, revision?.Number, poc.State, revision?.RolloutState,
            revision?.RolloutAtTicks is { } ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null,
            healthy && !string.IsNullOrWhiteSpace(host) ? $"https://{host}" : null, revision?.PipelineUrl, revision?.CommitSha,
            revision?.RolloutCommitSha, CommitUrl(repoUrl, revision?.RolloutCommitSha));
    }

    private static string? CommitUrl(string? repoUrl, string? sha)
    {
        if (sha is null || !Uri.TryCreate(repoUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https")) return null;
        var web = url.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return $"{(web.EndsWith(".git", StringComparison.Ordinal) ? web[..^4] : web)}/commit/{sha}";
    }
}
