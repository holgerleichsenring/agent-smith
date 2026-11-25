using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-24-f9c5d: a merge-green run's pull requests that reported no CI (<see cref="CiGreenPullRequest.NoCi"/>) have their
/// repository's pull-request CI started (<see cref="IPullRequestCiTrigger"/>). Each answer is said on this run (the series page
/// reads it as what the merge said) and on the attempt. Without a trigger factory (a process with no hosts) nothing is started.
/// 2026-11-25-9992e: the series master asks for it (ci "start"); nothing merges with it, so a repository with no CI at all is
/// said as such and left to the master's merge.
/// </summary>
public sealed class PullRequestCiStart(PullRequestCompleter completer, ILogger<PullRequestCiStart> logger, IPullRequestCiTriggerFactory? triggers = null)
{
    /// <summary>What each start answered, per repository.</summary>
    public async Task<string> StartAsync(CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var answers = new List<(CiGreenPullRequest Pr, CiTrigger Answer)>();
        foreach (var pr in request.PullRequests.Where(p => p.NoCi))
            answers.Add((pr, await OneAsync(context.Configs.FirstOrDefault(r => r.Name == pr.Repo), pr, ct)));
        foreach (var (pr, answer) in answers)
        {
            // On this run as a refusal with the sentence — what the series page shows as said — and on the attempt.
            var entry = new OpenedPullRequest(pr.Repo, pr.Url, OpenStatus.Opened);
            await completer.PublishAsync(context.Pipeline, entry, AgentSmith.Contracts.Providers.PullRequestCompletion.Refused(Sentence(answer)), ct);
            await completer.PublishOnAsync(request.AttemptRunId, entry, PullRequestStatuses.AwaitingCi, Sentence(answer), ct);
        }
        return string.Join("; ", answers.Select(a => $"{a.Pr.Repo}: {a.Answer.Text}"));
    }

    private async Task<CiTrigger> OneAsync(RepoConnection? repo, CiGreenPullRequest pr, CancellationToken ct)
    {
        if (repo is null || triggers?.Create(repo) is not { } trigger)
            return CiTrigger.Impossible("its host has no API that starts a pull request's CI; merge it by hand once it is checked");
        var answer = await trigger.StartAsync(pr.Url, repo.DefaultBranch, ct);
        logger.LogInformation("{Repo}: CI for {Url}: {Outcome} — {Text}", pr.Repo, pr.Url, answer.Outcome, answer.Text);
        return answer;
    }

    // What the attempt says for a pull request: the start, why there is none, or that it has no CI anywhere.
    private static string Sentence(CiTrigger answer) => answer.Outcome switch
    {
        CiTriggerOutcome.Started => $"CI started: {answer.Text}" + (answer.Url is { } url ? $" ({url})" : string.Empty),
        CiTriggerOutcome.Impossible => $"CI not started: {answer.Text}",
        _ => $"no CI: {answer.Text}",
    };
}
