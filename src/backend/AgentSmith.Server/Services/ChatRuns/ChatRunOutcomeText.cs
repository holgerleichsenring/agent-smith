using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.ChatRuns;

/// <summary>
/// What a thread is told when its run is over: delivered with the pull requests it opened,
/// failed with the reason its record gives, ended otherwise with the status, or gone when the
/// run left no record at all. Each ends with the dashboard link when one is configured.
/// </summary>
public sealed class ChatRunOutcomeText(ChatRunLink runLink)
{
    public string Compose(string runId, RunOutcome? outcome)
    {
        var body = outcome switch
        {
            null => $":grey_question: Run `{runId}` left no record — it never started, or its record "
                    + "was deleted. This thread can start a run again.",
            { Status: var s } when RunStatuses.IsDelivered(s) => Delivered(runId, outcome),
            { Status: RunStatuses.Failed } =>
                $":x: Run `{runId}` failed: {Reason(outcome)}",
            _ => $":warning: Run `{runId}` ended {outcome.Status}: {Reason(outcome)}",
        };
        return runLink.For(runId) is { } link ? $"{body}\nDetails: {link}" : body;
    }

    private static string Delivered(string runId, RunOutcome outcome)
    {
        var head = $":white_check_mark: Run `{runId}` finished ({outcome.Status}).";
        if (!string.IsNullOrWhiteSpace(outcome.Summary)) head += $" {outcome.Summary}";
        return outcome.PullRequestUrls.Count == 0
            ? head
            : head + "\n" + string.Join("\n", outcome.PullRequestUrls.Select(url => $":link: {url}"));
    }

    private static string Reason(RunOutcome outcome) =>
        string.IsNullOrWhiteSpace(outcome.Summary) ? "no reason was recorded" : outcome.Summary;
}
