using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-9992e: the series master's acts on pull requests — merge, refresh, retarget and CI — for a member ticket's pull
/// requests (<see cref="ISeriesPullRequestActs"/>) and, named "final", the series' final pull requests (<see cref="SeriesFinalMerge"/>,
/// <see cref="SeriesFinalCi"/>). The master owns these for its tickets: no server path does them on its own for a ticket of an
/// active series. Each takes the master's reason; a write is said with it (<see cref="SeriesActSay"/>), every call is logged.
/// </summary>
public sealed class SeriesPullRequestTools(
    SeriesBeat beat, SeriesMasterContext context, ISeriesPullRequestActs acts, SeriesFinalMerge finals, SeriesFinalCi finalCi)
{
    public const string Final = "final";
    public const string CiTool = "ci";
    public const string MergeTool = "merge_pull_request"; // 2026-11-25-433fa: the merged milestone tells the master's own merge by it

    public IEnumerable<AIFunction> GetTools() =>
    [
        AIFunctionFactory.Create(MergePullRequestAsync, MergeTool),
        AIFunctionFactory.Create(RefreshBranchAsync, "refresh_branch"),
        AIFunctionFactory.Create(RetargetPullRequestAsync, "retarget_pull_request"),
        AIFunctionFactory.Create(CiAsync, CiTool),
    ];

    [Description("Merges a ticket's waiting pull requests, or with ticket 'final' the series' final pull requests into the default branch. "
        + "how 'now' merges at once past branch protection where agent-smith's identity may; 'auto' arms the host's auto-merge, which completes once "
        + "the host's rules pass (a series branch has no rules, so there it merges at once). It merges whatever its prerequisites, CI or base: you judge "
        + "when; the host's own refusal comes back as the answer.")]
    public Task<string> MergePullRequestAsync([Description("The ticket's key or id, or 'final'.")] string ticket,
        [Description("'now' or 'auto'.")] string how, [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default)
    {
        if (How(how) is not { } protection) return Task.FromResult($"'{how}' is no way to merge; say now or auto.");
        var did = $" ({how.Trim().ToLowerInvariant()})";
        return IsFinal(ticket) ? AllowedAsync("merged the final pull requests" + did, reason, () => finals.MergeAsync(context, beat.Record, protection, ct))
            : OnTicketAsync(ticket, key => $"merged {key}{did}", reason, id => acts.MergeAsync(beat.Record, id, protection, ct));
    }

    [Description("Brings a ticket's open pull requests up to date with their base through a refresh-base run, or with ticket 'final' the series "
        + "branch under the final pull requests — for a conflict, or a base that moved on. A refresh already at work is said, not doubled.")]
    public Task<string> RefreshBranchAsync([Description("The ticket's key or id, or 'final'.")] string ticket,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default) =>
        IsFinal(ticket) ? AllowedAsync("refreshed the series branch", reason, () => finals.RefreshAsync(context, beat.Record, ct))
            : OnTicketAsync(ticket, key => $"refreshed {key}", reason, id => acts.RefreshAsync(beat.Record, id, ct));

    [Description("Moves a ticket's open pull request in one repository onto another target branch through a retarget run — for one stacked on a "
        + "prerequisite's branch that has landed, or one that should go into the series branch or the default branch.")]
    public Task<string> RetargetPullRequestAsync([Description("The ticket's key or id.")] string ticket,
        [Description("The repository of the pull request.")] string repo, [Description("The branch it should target.")] string target,
        [Description(SeriesActSay.Why)] string? reason = null, CancellationToken ct = default) =>
        OnTicketAsync(ticket, key => $"retargeted {key} in {repo} onto {target}", reason, id => acts.RetargetAsync(beat.Record, id, repo, target, ct));

    [Description("A ticket's pull-request CI, or with ticket 'final' the final pull requests': act 'read' reads the pull requests from their hosts now "
        + "and answers each one's CI on its head; 'requeue' queues the required builds that have not started again; 'start' starts the "
        + "pull-request CI of a repository whose host reported none.")]
    public Task<string> CiAsync([Description("The ticket's key or id, or 'final'.")] string ticket,
        [Description("'read', 'requeue' or 'start'.")] string act, [Description(SeriesActSay.Why)] string? reason = null,
        CancellationToken ct = default)
    {
        var what = (act ?? string.Empty).Trim().ToLowerInvariant();
        if (what is not (SeriesCiActs.Read or SeriesCiActs.Requeue or SeriesCiActs.Start)) return Task.FromResult(SeriesCiActs.Unknown(act ?? string.Empty));
        Func<Task<string>> ci;
        string name;
        if (IsFinal(ticket)) (ci, name) = (() => finalCi.ActAsync(context, beat.Record, what, ct), "the final pull requests");
        else if (SeriesTicketLookup.Find(beat, ticket) is { } fact)
            (ci, name) = (() => acts.CiAsync(beat.Record, fact.Ticket.TicketId, what, ct), fact.Ticket.Key);
        else return Task.FromResult(SeriesTicketLookup.Unknown(beat, ticket));
        if (what != SeriesCiActs.Read) return AllowedAsync($"{(what == SeriesCiActs.Start ? "started" : "requeued")} the CI of {name}", reason, ci);
        beat.ActDid = $"read the CI of {name}"; // a read changes nothing on the host, so it is not said
        return ci();
    }

    private static bool IsFinal(string ticket) => string.Equals(ticket?.Trim(), Final, StringComparison.OrdinalIgnoreCase);

    private static BranchProtection? How(string how) => (how ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "now" => BranchProtection.Bypass,
        "auto" => BranchProtection.Respect,
        _ => null,
    };

    private Task<string> OnTicketAsync(string ticket, Func<string, string> did, string? reason, Func<string, Task<string>> act) =>
        SeriesTicketLookup.Find(beat, ticket) is not { } fact ? Task.FromResult(SeriesTicketLookup.Unknown(beat, ticket))
            : AllowedAsync(did(fact.Ticket.Key), reason, () => act(fact.Ticket.TicketId));

    // A write takes the approver's runs.control, as every act of the series does; 9992h: it is said with its reason.
    private async Task<string> AllowedAsync(string did, string? reason, Func<Task<string>> act) => beat.Record.MayStartRuns
        ? SeriesActSay.Acted(beat, did, reason, await act())
        : $"The series' approver {beat.Record.Principal} may not start runs (runs.control), so the series changes no pull request.";
}
