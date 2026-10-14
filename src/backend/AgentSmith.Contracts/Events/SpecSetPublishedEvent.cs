using AgentSmith.Contracts.Specs;

namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-10-13-9a59d: a spec set was published once its placement held — written or not. It records
/// the revision the attempt works from (number and cause as written), the series id, the executed
/// spec ids it carried in, the approval it carries (instant, conversation, principal) and whether the
/// write committed. A publish with no <see cref="RevisionNumber"/> carries no revision and leaves the
/// attempt's recorded revision untouched.
/// <para>2026-10-13-9a59e: it carries the CUT — the set's key, the hand-back case (the one writer of an
/// attempt's case; null when the set was not handed back) and every spec as published. A hand-back that
/// never reached the publisher is announced with the ticket key, no specs and its case.</para>
/// </summary>
public sealed record SpecSetPublishedEvent(
    string RunId,
    DateTimeOffset Timestamp,
    int? RevisionNumber = null,
    string? RevisionCause = null,
    string? SeriesId = null,
    IReadOnlyList<string>? CarriedSpecIds = null,
    DateTimeOffset? ApprovalAt = null,
    string? ApprovalConversation = null,
    string? ApprovalPrincipal = null,
    bool Committed = false,
    string? SetKey = null,
    string? HandbackCase = null,
    IReadOnlyList<SpecCutEntry>? Specs = null)
    : RunEvent(RunId, EventType.SpecSetPublished, Timestamp);
