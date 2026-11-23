using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;
using static AgentSmith.Application.Services.Holds.TicketHoldOf;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-31dcg: the one hold of a ticket, read from its stored facts — one method per source, and when several apply
/// the most blocking wins: admin, then decide, then plan, then merge, then nobody. A finished ticket has none, and so has a
/// ticket nothing waits on (a filed one, a cancelled one, one whose pull requests all closed).
/// 2026-11-23-31dch: a parked question holds by who can answer it (<see cref="TicketQuestionHold"/>).
/// <para>2026-11-23-31dci: a nobody hold says since when it waits, and a wait on something outside the run (the model place,
/// sandbox capacity, the restart) that outlasts <see cref="TicketHoldFacts.EscalateAfterMinutes"/> is the admin's
/// (<see cref="TicketHoldEscalations"/>) — decided when the hold is read, so nothing runs in the background or is stored.</para>
/// <para>2026-11-23-d60d: an open pull request always holds (<see cref="TicketPullRequestHold"/>); no in-review state.</para>
/// </summary>
public sealed class TicketHoldReader
{
    /// <summary>Where the trusted image registries are set — the act of an untrusted image without a stored setting path.</summary>
    public const string SandboxSettingsPath = "/config/settings/sandbox";

    public TicketHold? Of(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Finished) return null;
        return Admin(facts) ?? Decide(facts) ?? Plan(facts) ?? Merge(facts) ?? TicketHoldEscalations.Of(Nobody(facts), facts);
    }

    /// <summary>A precondition stopped the newest attempt — an image, a bootstrap or another configured precondition — or
    /// the parked question names the environment.</summary>
    public TicketHold? Admin(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (TicketQuestionHold.Environment(facts) is { } reported) return reported;
        if (facts.HeadStatus != RunStatuses.Failed || !RunEndKinds.IsPrecondition(facts.EndKind)) return null;
        return facts.EndKind switch
        {
            RunEndKinds.ImageUntrusted => Hold(facts, TicketHoldAddressees.Admin, TicketHoldCases.ImageUntrusted,
                new(TicketHoldActs.Settings, facts.SettingsPath ?? SandboxSettingsPath), facts.EndedAt),
            RunEndKinds.BootstrapMissing => Hold(facts, TicketHoldAddressees.Admin, TicketHoldCases.BootstrapMissing,
                new(TicketHoldActs.Init, facts.Project), facts.EndedAt),
            _ => Hold(facts, TicketHoldAddressees.Admin, TicketHoldCases.Preflight,
                facts.SettingsPath is { } path ? new(TicketHoldActs.Settings, path) : new(TicketHoldActs.Conversation), facts.EndedAt),
        };
    }

    /// <summary>A parked question or hand-back, or a newest attempt that failed or fell short and that no restart takes up.
    /// 2026-11-23-d60d: or red CI on an open pull request of a ticket not at work.</summary>
    public TicketHold? Decide(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Parked)
            return facts.Asked
                ? TicketQuestionHold.Target(facts)
                : Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Handback, new(TicketHoldActs.Conversation), facts.ParkedAt);
        var retry = new TicketHoldAct(TicketHoldActs.Retry, facts.HeadRunId);
        if (facts.Interrupted && !facts.Restarting)
            return Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Interrupted, retry, facts.EndedAt);
        if (facts.HeadStatus == RunStatuses.Shortfall)
            return Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Shortfall, new(TicketHoldActs.Conversation), facts.EndedAt);
        if (facts.HeadStatus != RunStatuses.Failed) return TicketPullRequestHold.CiFailed(facts);
        var rework = new TicketHoldAct(TicketHoldActs.Conversation);
        if (facts.EndKind == RunEndKinds.ReadinessUnknown)
            return Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.ReadinessUnknown, retry, facts.EndedAt);
        if (facts.EndedOnInfrastructure)
            return Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Infrastructure, retry, facts.EndedAt);
        return facts.EndKind == RunEndKinds.CriteriaUnmet
            ? Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.CriteriaUnmet, rework, facts.EndedAt)
            : Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Failed, rework, facts.EndedAt);
    }

    /// <summary>A plan question its series settles, or a person edited the ticket's text after its specs were approved and
    /// the conversation has not answered it.</summary>
    public TicketHold? Plan(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (TicketQuestionHold.Plan(facts) is { } plan) return plan;
        return facts.TextEdited
            ? Hold(facts, TicketHoldAddressees.Plan, TicketHoldCases.TextEdited, new(TicketHoldActs.Conversation), null)
            : null;
    }

    /// <summary>The ticket's pull request merges next in its series, or a never-run ticket starts once others are merged. A
    /// ticket at work is not held by its pull request. 2026-11-23-d60d: nor one whose CI still runs; one outside any series'
    /// merge order merges now when nothing it requires is unlanded, by opening the pull request.</summary>
    public TicketHold? Merge(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.StartsOnceMerged.Count > 0)
            return Hold(facts, TicketHoldAddressees.Merge, TicketHoldCases.StartsOnMerge,
                facts.SeriesId is { } series ? new(TicketHoldActs.MergeNext, series) : null, null) with { Names = facts.StartsOnceMerged };
        if (AtWork(facts) || TicketPullRequestHold.CiHolds(facts)) return null;
        return facts.MergeStep is { Wait: SeriesRunMergeWaits.Ready } && facts.SeriesId is { } id
            ? Hold(facts, TicketHoldAddressees.Merge, TicketHoldCases.MergeNext, new(TicketHoldActs.Merge, id), facts.EndedAt)
            : TicketPullRequestHold.Ready(facts);
    }

    /// <summary>The ticket moves on by itself: it works or waits for its model place (31dci), waits in the queue or resumes,
    /// the restart takes it up, its pull request merges after another, or what it requires is still worked. 31dci: a wait
    /// says since when — the model wait's start, the queue arrival, the interruption's end, the specs' approval.
    /// 2026-11-23-d60d: and a pull request that waits on nobody says why (<see cref="TicketPullRequestHold.Waiting"/>).</summary>
    public TicketHold? Nobody(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var none = (string @case, DateTimeOffset? since) => Hold(facts, TicketHoldAddressees.Nobody, @case, null, since);
        switch (facts.HeadStatus)
        {
            case RunStatuses.Running:
                return facts.WaitingForModelSince is { } waiting ? none(TicketHoldCases.ModelWait, waiting) : none(TicketHoldCases.Working, null);
            case RunStatuses.Queued: return none(TicketHoldCases.Queued, facts.EnqueuedAt);
            case RunStatuses.WaitingForInput when !facts.Parked: return none(TicketHoldCases.Resuming, facts.EnqueuedAt);
        }
        if (facts.Interrupted && facts.Restarting) return none(TicketHoldCases.Restarting, facts.EndedAt);
        if (facts.MergeStep is { Wait: SeriesRunMergeWaits.After } after)
            return none(TicketHoldCases.MergeAfter, facts.EndedAt) with { Names = after.After };
        if (facts.MergeStep is { Wait: SeriesRunMergeWaits.Retarget }) return none(TicketHoldCases.Retarget, facts.EndedAt);
        if (TicketPullRequestHold.Waiting(facts) is { } waits) return waits;
        return facts.WaitsFor.Count > 0 ? none(TicketHoldCases.Prerequisite, facts.FiledAt) with { Names = facts.WaitsFor } : null;
    }
}
