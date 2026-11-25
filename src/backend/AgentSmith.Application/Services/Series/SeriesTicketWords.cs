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
        H.Question => "question",
        H.QuestionPlan => "plan question",
        H.QuestionEnvironment => "question for the admin",
        H.Handback => "handed back",
        H.Interrupted => "red · interrupted, not restarted by itself",
        H.Failed or H.CriteriaUnmet or H.Infrastructure or H.ReadinessUnknown or H.Shortfall =>
            $"red · {(fact.EndDetail is { Length: > 0 } why ? why : Spoken(hold.Case))}",
        H.ImageUntrusted or H.BootstrapMissing or H.Preflight => $"stopped · {Spoken(hold.Case)} (admin)",
        H.CiFailed => $"CI red · {fact.CiFailure ?? (hold.Names.Count > 0 ? string.Join(", ", hold.Names) : "a check failed")}",
        H.CiRunning => "CI running" + Since(hold),
        H.CiNotStarted => "build not started" + Since(hold),
        H.PrConflict => "PR conflicts with its base",
        H.RefreshRefused => $"refresh left the branch unchanged · {hold.Detail?.Subject}",
        H.MergeNext => "ready to merge · next",
        H.MergeReady => "ready to merge",
        H.MergeAfter => $"merges after {Names(hold)}",
        H.Retarget => "PR moves onto the default branch first",
        H.StartsOnMerge => $"starts once {Names(hold)} {(hold.Names.Count == 1 ? "is" : "are")} merged",
        H.Prerequisite => $"waits on {Names(hold)}",
        H.TextEdited => "text edited · the plan is settled in the conversation",
        _ => Spoken(hold.Case),
    };

    private static string Since(TicketHold hold) => hold.Since is { } at ? $" since {at.UtcDateTime.ToString("d MMM HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC" : string.Empty;

    private static string Names(TicketHold hold) => hold.Names.Count == 0 ? "others" : string.Join(", ", hold.Names);

    private static string Spoken(string @case) => @case.Replace('_', ' ');
}
