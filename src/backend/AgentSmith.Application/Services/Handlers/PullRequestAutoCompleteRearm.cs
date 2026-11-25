using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-24-629ab: the pipeline step that arms a pull request's auto-complete again after its host dropped it at a conflict (GitLab: at a new head)
/// that has ended (<see cref="IPullRequestAutoCompleteRearmer"/>). A host without one, a local repository or a host refusal
/// changes nothing and says why; it never fails the run.
/// </summary>
public sealed class PullRequestAutoCompleteRearm(ISourceProviderFactory sources, ILogger<PullRequestAutoCompleteRearm> logger)
{
    /// <summary>What the host answered; refused when it has no auto-complete to arm.</summary>
    public async Task<PullRequestCompletion> RearmAsync(RepoConnection repo, string prUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (repo.Type == RepoType.Local || sources.Create(repo) is not IPullRequestAutoCompleteRearmer rearmer)
            return PullRequestCompletion.Refused("its host has no auto-complete agent-smith arms again");
        try
        {
            var answer = await rearmer.RearmAutoCompleteAsync(prUrl, ct);
            logger.LogInformation("{Url}: auto-complete armed again: {Outcome} {Reason}", prUrl, answer.Outcome, answer.Reason);
            return answer;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Url}: auto-complete could not be armed again", prUrl);
            return PullRequestCompletion.Refused(ex.Message);
        }
    }
}
