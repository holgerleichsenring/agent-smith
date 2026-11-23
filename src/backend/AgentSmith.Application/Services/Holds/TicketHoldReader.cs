using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-31dcg: the one hold of a ticket, read from its stored facts — one method per source, and when several apply
/// the most blocking wins: admin, then decide, then plan, then merge, then nobody. A finished ticket has none, and so has a
/// ticket nothing waits on (a filed one, a cancelled one, one whose pull requests all closed).
/// <para>2026-11-23-31dci: a nobody hold says since when it waits, and a wait on something outside the run (the model place,
/// sandbox capacity, the restart) that outlasts <see cref="TicketHoldFacts.EscalateAfterMinutes"/> is the admin's
/// (<see cref="TicketHoldEscalations"/>) — decided when the hold is read, so nothing runs in the background or is stored.</para>
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

    /// <summary>A precondition stopped the newest attempt: an image, a bootstrap or another configured precondition.</summary>
    public TicketHold? Admin(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
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

    /// <summary>A parked question or hand-back, or a newest attempt that failed or fell short and that no restart takes up.</summary>
    public TicketHold? Decide(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Parked)
            return facts.Asked
                ? Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Question, new(TicketHoldActs.Answer), facts.ParkedAt)
                : Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Handback, new(TicketHoldActs.Conversation), facts.ParkedAt);
        var retry = new TicketHoldAct(TicketHoldActs.Retry, facts.HeadRunId);
        if (facts.Interrupted && !facts.Restarting)
            return Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Interrupted, retry, facts.EndedAt);
        if (facts.HeadStatus == RunStatuses.Shortfall)
            return Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Shortfall, new(TicketHoldActs.Conversation), facts.EndedAt);
        if (facts.HeadStatus != RunStatuses.Failed) return null;
        var rework = new TicketHoldAct(TicketHoldActs.Conversation);
        if (facts.EndKind == RunEndKinds.ReadinessUnknown)
            return Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.ReadinessUnknown, retry, facts.EndedAt);
        if (facts.EndedOnInfrastructure)
            return Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Infrastructure, retry, facts.EndedAt);
        return facts.EndKind == RunEndKinds.CriteriaUnmet
            ? Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.CriteriaUnmet, rework, facts.EndedAt)
            : Hold(facts, TicketHoldAddressees.Decide, TicketHoldCases.Failed, rework, facts.EndedAt);
    }

    /// <summary>A person edited the ticket's text after its specs were approved, and the conversation has not answered it.</summary>
    public TicketHold? Plan(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.TextEdited
            ? Hold(facts, TicketHoldAddressees.Plan, TicketHoldCases.TextEdited, new(TicketHoldActs.Conversation), null)
            : null;
    }

    /// <summary>The ticket's pull request merges next in its series, or a never-run ticket starts once others are merged. A
    /// ticket at work is not held by its pull request; one outside any series has no merge order and stays in review.</summary>
    public TicketHold? Merge(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.StartsOnceMerged.Count > 0)
            return Hold(facts, TicketHoldAddressees.Merge, TicketHoldCases.StartsOnMerge,
                facts.SeriesId is { } series ? new(TicketHoldActs.MergeNext, series) : null, null) with { Names = facts.StartsOnceMerged };
        if (AtWork(facts)) return null;
        return facts.MergeStep is { Wait: SeriesRunMergeWaits.Ready } && facts.SeriesId is { } id
            ? Hold(facts, TicketHoldAddressees.Merge, TicketHoldCases.MergeNext, new(TicketHoldActs.Merge, id), facts.EndedAt)
            : null;
    }

    /// <summary>The ticket moves on by itself: it works or waits for its model place (31dci), waits in the queue or resumes,
    /// the restart takes it up, its pull request merges after another, or what it requires is still worked. 31dci: a wait
    /// says since when — the model wait's start, the queue arrival, the interruption's end, the specs' approval.</summary>
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
            return none(TicketHoldCases.MergeAfter, null) with { Names = after.After };
        if (facts.MergeStep is { Wait: SeriesRunMergeWaits.Retarget }) return none(TicketHoldCases.Retarget, null);
        return facts.WaitsFor.Count > 0 ? none(TicketHoldCases.Prerequisite, facts.FiledAt) with { Names = facts.WaitsFor } : null;
    }

    private static bool AtWork(TicketHoldFacts facts) =>
        facts.HeadStatus is RunStatuses.Running or RunStatuses.Queued || (facts.HeadStatus == RunStatuses.WaitingForInput && !facts.Parked);

    // A stop's hold quotes what the stop recorded; a parked or a moving ticket's names no end.
    private static TicketHold Hold(TicketHoldFacts facts, string addressee, string @case, TicketHoldAct? act, DateTimeOffset? since) =>
        new(addressee, @case, act, since)
        {
            Attempt = facts.Attempt,
            Detail = addressee is TicketHoldAddressees.Admin or TicketHoldAddressees.Decide && !facts.Parked ? facts.EndDetail : null,
        };
}
