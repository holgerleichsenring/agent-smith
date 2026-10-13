namespace AgentSmith.Domain.Models;

/// <summary>2026-10-13-9a59o: why an attempt started, as its run recorded it.</summary>
/// <param name="Channel">ticket_comment | pull_request | status_move | requested | retrigger |
/// pickup | operator | retry, passed verbatim.</param>
/// <param name="SourceKind">comment | review | command | status | conversation.</param>
public sealed record TicketRunCause(string Channel, string? Actor, DateTimeOffset? At, string? SourceKind);

/// <summary>
/// 2026-10-13-9a59o: one spec a run worked, with what its acceptance judge said about it. The three
/// accounts are null when the spec has no stored judgement or the stored one cannot be read.
/// </summary>
public sealed record TicketRunSpec(
    string Id, string Status, string? Verdict,
    IReadOnlyList<TicketRunCriterion>? Criteria,
    IReadOnlyList<TicketRunDeclined>? Declined,
    IReadOnlyList<TicketRunUnmeasured>? Unmeasured);

/// <param name="Status">met | unmet | not_applicable | unproven.</param>
public sealed record TicketRunCriterion(string Criterion, string Status, string? Note, string? Citation);

/// <summary>A criterion the agent declined, with the evaluated meaning of not doing it.</summary>
public sealed record TicketRunDeclined(string Criterion, string Reason);

/// <summary>An account nobody could take — a red build, a diff that would not run.</summary>
public sealed record TicketRunUnmeasured(string Repos, string? Reason);

/// <summary>2026-10-13-9a59o: one pull request's record, named by its readable url.</summary>
public sealed record TicketPullRequestReview(
    string Url, string State, DateTimeOffset? StateAt, IReadOnlyList<TicketReviewEntry> Entries);

/// <summary>
/// 2026-10-13-9a59o: one comment, review or decision on a pull request. An entry with
/// <paramref name="Trusted"/> false started nothing; a <paramref name="System"/> entry is the
/// host's own event, not a person's.
/// </summary>
/// <param name="Truncated">The body was cut — in this answer or already in the store.</param>
public sealed record TicketReviewEntry(
    string Kind, string? Decision, bool Standing, string? Author, bool Trusted, bool Ours, bool Bot,
    bool System, string? File, int? Line, bool? Resolved, string Body, bool Truncated, DateTimeOffset At);
