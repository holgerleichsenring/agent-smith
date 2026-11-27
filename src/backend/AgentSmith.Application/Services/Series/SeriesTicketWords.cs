using AgentSmith.Application.Services.Holds;
using H = AgentSmith.Application.Services.Holds.TicketHoldCases;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-25-9992a: a held series ticket's state in words, one phrase per hold case — what the master's table,
/// the dialog panel and the series page's title say about it. A wait names since when it holds, never for how long, so equal
/// facts give equal words and the beat's snapshot moves only when the ticket does.</summary>
internal static class SeriesTicketWords
{
    internal static string Of(TicketHold hold, SeriesTicketFact fact) => hold.Case switch
    {
        H.Working => "coding",
        H.ModelWait => "waiting for its model place" + Since(hold),
        H.Queued => "queued" + Since(hold),
        H.Resuming => "resuming",
        H.Restarting => "interrupted · restarting",
        H.Refreshing => "refreshing its pull request's branch",
        H.Question => hold.Addressee == TicketHoldAddressees.Nobody ? "question · the series master's" : "question", // 3532b
        H.QuestionPlan => "plan question",
        H.QuestionEnvironment => "question for the admin",
        H.Handback => "handed back",
        H.Interrupted => "red · interrupted, not restarted by itself",
        H.Failed or H.CriteriaUnmet or H.Infrastructure or H.ReadinessUnknown or H.Shortfall =>
            $"red · {(fact.EndDetail is { Length: > 0 } why ? why : Spoken(hold.Case))}",
        H.ImageUntrusted or H.BootstrapMissing or H.Preflight => $"stopped · {Spoken(hold.Case)} (admin)",
        H.CiFailed => $"CI red · {fact.CiFailure ?? (hold.Names.Count > 0 ? string.Join(", ", hold.Names) : "a check failed")}",
        H.ApprovalNeeded => "needs a person's approval on the host" + Pr(hold), // 2026-11-26-5201f
        H.PrDraft => "PR is still a draft · the host asks nobody to approve it; publish it (merge_pull_request publishes first) only once "
            + "its work is complete, its description says what a stopped run left out" + Pr(hold),
        H.CiRunning => "CI running" + Since(hold),
        H.CiNotStarted => "build not started" + Since(hold),
        H.PrConflict => "PR conflicts with its base",
        H.MasterRefresh => "PR conflicts with its base · yours: refresh_branch, no person waits on it", // 2026-11-27-e451
        H.MasterDraft => "PR is still a draft · yours: publish it (merge_pull_request publishes first) only once its work is "
            + "complete, its description says what a stopped run left out" + Pr(hold), // 2026-11-27-e451
        H.RefreshRefused => $"refresh left the branch unchanged · {hold.Detail?.Subject}",
        H.MergeNext => "ready to merge · next",
        H.Merging => "ready to merge · next · the master's merge, no person waits on it", // 2026-11-27-e451
        H.MergeReady => "ready to merge",
        H.MergeAfter => $"merges after {Names(hold)}",
        H.Retarget => $"PR targets {Targets(fact)}, neither the series nor the default branch · move it with retarget_pull_request", // 5201f
        H.StartsOnMerge => $"starts once {Names(hold)} {(hold.Names.Count == 1 ? "is" : "are")} merged",
        H.Prerequisite => $"waits on {Names(hold)}",
        H.TextEdited => "text edited · the plan is settled in the conversation",
        H.StartRefused => "start refused · the series' approver may not start runs; a person starts it", // 2026-11-27-ecf2e
        _ => Spoken(hold.Case),
    };

    private static string Since(TicketHold hold) => hold.Since is { } at ? $" since {at.UtcDateTime.ToString("d MMM HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC" : string.Empty;

    private static string Pr(TicketHold hold) => hold.Act?.Target is { } pr ? $" · {pr}" : string.Empty;

    private static string Targets(SeriesTicketFact fact) =>
        fact.Waiting.Select(p => p.Target).OfType<string>().Distinct(StringComparer.Ordinal).ToList() is { Count: > 0 } targets
            ? string.Join(", ", targets) : "another branch";

    private static string Names(TicketHold hold) => hold.Names.Count == 0 ? "others" : string.Join(", ", hold.Names);

    private static string Spoken(string @case) => @case.Replace('_', ' ');
}
