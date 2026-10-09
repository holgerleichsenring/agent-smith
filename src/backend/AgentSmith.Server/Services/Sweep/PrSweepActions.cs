using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-10b0: what a pull-request event starts, from the PR sweep — the same routes, launcher
/// and admission the webhooks use, limited to the polling entry's projects. Azure DevOps has no
/// label scan in either mode: its webhook carries no label change.
/// </summary>
public sealed class PrSweepActions(
    PrReviewRouteResolver routes,
    PrTriggerLabelResolver labels,
    PrRunContextFactory contexts,
    AgentSmith.Server.Contracts.IDetachedPipelineLauncher launcher,
    PrCommentCommandAdmission admission,
    IServiceProvider services)
{
    private const string ScanPipeline = "security-scan";

    public async Task ReviewAsync(SweepTarget target, OpenPullRequest pr)
    {
        if (routes.Resolve(target.Config, Kind(target.Repo), target.Repo.Url!, pr.Labels) is { } route && target.Allowed.Contains(route.ProjectName))
            await launcher.LaunchAsync(route.ProjectName, route.PipelineName, contexts.FromFacts(Facts(pr), route.RepoName));
    }

    public async Task ScanAsync(SweepTarget target, OpenPullRequest pr)
    {
        if (target.Repo.Type == RepoType.AzureDevOps) return;
        if (labels.Match(target.Config, Kind(target.Repo), target.Repo.Url!, pr.Labels) is { ProjectName: { } owner } && target.Allowed.Contains(owner))
            await launcher.LaunchAsync(owner, ScanPipeline, contexts.FromFacts(Facts(pr), target.Repo.Name));
    }

    public Task CommandAsync(SweepTarget target, PrSweepComment comment, CancellationToken ct) =>
        admission.AdmitAsync(new PrCommentCommand(comment.Body, comment.Author, $"{target.Repo.Name}#{comment.PrNumber}",
                $"pr:{target.Repo.Name}#{comment.PrNumber}", comment.PrNumber),
            services.GetRequiredKeyedService<IPrCommentAuthorTrust>(Kind(target.Repo)), ct, target.Allowed);

    public static string Kind(RepoConnection repo) => repo.Type switch
    {
        RepoType.GitHub => "github",
        RepoType.GitLab => "gitlab",
        _ => "azuredevops",
    };

    private static PullRequestFacts Facts(OpenPullRequest pr) => new(pr.Number, pr.HeadSha, pr.BaseSha, pr.Author, pr.HeadBranch);
}
