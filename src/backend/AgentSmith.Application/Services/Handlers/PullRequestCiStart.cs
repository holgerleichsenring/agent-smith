using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-24-f9c5d: before a merge-green run merges, the pull requests into a series branch that reported no CI
/// (<see cref="CiGreenPullRequest.NoCi"/>) have their repository's pull-request CI started (<see cref="IPullRequestCiTrigger"/>).
/// Started and impossible are said on this run (the series page reads it as what the merge said) and on the attempt as what
/// keeps each awaiting CI, and nothing
/// merges; a repository with no CI at all merges as a default-branch pull request with no CI does, but only when every pull
/// request of the request is such a one. Without a trigger factory (a process with no hosts) nothing is started.
/// </summary>
public sealed class PullRequestCiStart(PullRequestCompleter completer, ILogger<PullRequestCiStart> logger, IPullRequestCiTriggerFactory? triggers = null)
{
    /// <summary>Whether the merge may go ahead: no pull request needed a start, or none of them has CI anywhere.</summary>
    public async Task<(bool MayMerge, string Said)> StartAsync(CompletePullRequestsContext context, CiGreenMergeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        var none = request.PullRequests.Where(p => p.NoCi).ToList();
        if (none.Count == 0) return (true, string.Empty);
        var answers = new List<(CiGreenPullRequest Pr, CiTrigger Answer)>();
        foreach (var pr in none) answers.Add((pr, await OneAsync(context.Configs.FirstOrDefault(r => r.Name == pr.Repo), pr, ct)));
        var mayMerge = none.Count == request.PullRequests.Count && answers.All(a => a.Answer.Outcome == CiTriggerOutcome.NoCi);
        foreach (var (pr, answer) in answers.Where(a => !mayMerge))
        {
            // On this run as a refusal with the sentence — what the series page shows as said — and on the attempt as awaiting CI.
            var entry = new OpenedPullRequest(pr.Repo, pr.Url, OpenStatus.Opened);
            await completer.PublishAsync(context.Pipeline, entry, AgentSmith.Contracts.Providers.PullRequestCompletion.Refused(Sentence(answer, mayMerge)), ct);
            await completer.PublishOnAsync(request.AttemptRunId, entry, PullRequestStatuses.AwaitingCi, Sentence(answer, mayMerge), ct);
        }
        return (mayMerge, string.Join("; ", answers.Select(a => $"{a.Pr.Repo}: {a.Answer.Text}")));
    }

    private async Task<CiTrigger> OneAsync(RepoConnection? repo, CiGreenPullRequest pr, CancellationToken ct)
    {
        if (repo is null || triggers?.Create(repo) is not { } trigger)
            return CiTrigger.Impossible("its host has no API that starts a pull request's CI; merge it by hand once it is checked");
        var answer = await trigger.StartAsync(pr.Url, repo.DefaultBranch, ct);
        logger.LogInformation("{Repo}: CI for {Url}: {Outcome} — {Text}", pr.Repo, pr.Url, answer.Outcome, answer.Text);
        return answer;
    }

    // What the attempt says for a pull request whose merge waits: the start, why there is none, or why it waits for the others.
    private static string Sentence(CiTrigger answer, bool mayMerge) => answer.Outcome switch
    {
        CiTriggerOutcome.Started => $"CI started: {answer.Text}" + (answer.Url is { } url ? $" ({url})" : string.Empty),
        CiTriggerOutcome.Impossible => $"CI not started: {answer.Text}",
        _ when !mayMerge => $"no CI: {answer.Text}; the ticket's other pull requests have CI, so it merges by hand once they are green",
        _ => answer.Text,
    };
}
