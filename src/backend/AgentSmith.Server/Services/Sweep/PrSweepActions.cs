using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-10b0: what a pull-request event starts, from the PR sweep — the same launcher and
/// admission the webhooks use, limited to the polling entry's projects. Azure DevOps has no label
/// scan in either mode: its webhook carries no label change. 2026-10-13-0b82a: no review here — a
/// review starts on a person's command, through the admission.
/// </summary>
public sealed class PrSweepActions(
    PrTriggerLabelResolver labels,
    PrRunContextFactory contexts,
    AgentSmith.Server.Contracts.IDetachedPipelineLauncher launcher,
    PrCommentCommandAdmission admission,
    IServiceProvider services)
{
    private const string ScanPipeline = "security-scan";

    public async Task ScanAsync(SweepTarget target, OpenPullRequest pr)
    {
        if (target.Repo.Type == RepoType.AzureDevOps) return;
        if (labels.Match(target.Config, Kind(target.Repo), target.Repo.Url!, pr.Labels) is { ProjectName: { } owner } && target.Allowed.Contains(owner))
            await launcher.LaunchAsync(owner, ScanPipeline, contexts.FromFacts(Facts(pr), target.Repo.Name));
    }

    /// <summary>The comment on <paramref name="pr"/> through the admission — the pull request's head
    /// from the host's list, as a webhook's delivery carries it.</summary>
    public Task<WebhookResult> CommandAsync(SweepTarget target, PrSweepComment comment, OpenPullRequest pr, CancellationToken ct) =>
        admission.AdmitAsync(new PrCommentCommand(comment.Body, comment.Author, $"{target.Repo.Name}#{comment.PrNumber}",
                $"pr:{target.Repo.Name}#{comment.PrNumber}", comment.PrNumber)
            {
                HeadRef = pr.HeadBranch, SameRepository = pr.SameRepository, PrUrl = pr.Url,
            },
            services.GetRequiredKeyedService<IPrCommentAuthorTrust>(Kind(target.Repo)), ct, target.Allowed);

    public static string Kind(RepoConnection repo) => repo.Type switch
    {
        RepoType.GitHub => "github",
        RepoType.GitLab => "gitlab",
        _ => "azuredevops",
    };

    private static PullRequestFacts Facts(OpenPullRequest pr) => new(pr.Number, pr.HeadSha, pr.BaseSha, pr.Author, pr.HeadBranch);
}
