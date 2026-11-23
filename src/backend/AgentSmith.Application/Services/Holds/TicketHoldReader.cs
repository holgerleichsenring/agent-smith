using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-31dcg: the one hold of a ticket, read from its stored facts — one method per source, and when several apply
/// the most blocking wins: admin, then decide, then plan, then merge, then nobody. A finished ticket has none, and so has a
/// ticket nothing waits on (a filed one, a cancelled one, one whose pull requests all closed).
/// 2026-11-23-31dch: a parked question holds by who can answer it (<see cref="TicketQuestionHold"/>).
/// </summary>
public sealed class TicketHoldReader
{
    /// <summary>Where the trusted image registries are set — the act of an untrusted image without a stored setting path.</summary>
    public const string SandboxSettingsPath = "/config/settings/sandbox";

    public TicketHold? Of(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Finished) return null;
        return Admin(facts) ?? Decide(facts) ?? Plan(facts) ?? Merge(facts) ?? Nobody(facts);
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

    /// <summary>A parked question or hand-back, or a newest attempt that failed or fell short and that no restart takes up.</summary>
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

    /// <summary>The ticket moves on by itself: its attempt works, waits in the queue or resumes, the restart takes it up, its
    /// pull request merges after another, or what it requires is still being worked.</summary>
    public TicketHold? Nobody(TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var none = (string @case) => Hold(facts, TicketHoldAddressees.Nobody, @case, null, null);
        switch (facts.HeadStatus)
        {
            case RunStatuses.Running: return none(TicketHoldCases.Working);
            case RunStatuses.Queued: return none(TicketHoldCases.Queued);
            case RunStatuses.WaitingForInput when !facts.Parked: return none(TicketHoldCases.Resuming);
        }
        if (facts.Interrupted && facts.Restarting) return none(TicketHoldCases.Restarting);
        if (facts.MergeStep is { Wait: SeriesRunMergeWaits.After } after) return none(TicketHoldCases.MergeAfter) with { Names = after.After };
        if (facts.MergeStep is { Wait: SeriesRunMergeWaits.Retarget }) return none(TicketHoldCases.Retarget);
        return facts.WaitsFor.Count > 0 ? none(TicketHoldCases.Prerequisite) with { Names = facts.WaitsFor } : null;
    }

    private static bool AtWork(TicketHoldFacts facts) =>
        facts.HeadStatus is RunStatuses.Running or RunStatuses.Queued || (facts.HeadStatus == RunStatuses.WaitingForInput && !facts.Parked);

    // A stop's hold quotes what the stop recorded; a parked or a moving ticket's names no end.
    internal static TicketHold Hold(TicketHoldFacts facts, string addressee, string @case, TicketHoldAct? act, DateTimeOffset? since) =>
        new(addressee, @case, act, since)
        {
            Attempt = facts.Attempt,
            Detail = addressee is TicketHoldAddressees.Admin or TicketHoldAddressees.Decide && !facts.Parked ? facts.EndDetail : null,
        };
}
