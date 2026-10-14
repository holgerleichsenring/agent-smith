namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-8762p: a ticket's defaults — those its code runs took and those of the design conversation bound
/// to it — pending ones holding the series first, other pending ones next, decided ones last, newest first in each.
/// </summary>
public sealed record TicketDefaultsView(int Pending, int PendingHoldingSeries, IReadOnlyList<TicketDefaultView> Entries);

/// <summary>
/// 2026-10-13-8762p: one default. <see cref="Attempt"/> is the attempt number of its run, null for a run that is
/// not an attempt and for a design turn's row; <see cref="Conversation"/> names that design turn. A conversation
/// row's question, chosen answer, alternatives and reasons are null unless the caller may read the transcript.
/// <see cref="Landed"/> is set only on a memory proposal of a finished run: whether that run left a pull request.
/// </summary>
public sealed record TicketDefaultView(
    long Id, string RunId, int? Attempt, string? Conversation, string? SpecId,
    string? Question, string? Chosen, IReadOnlyList<string>? Alternatives, string? Why,
    bool HoldsSeries, string Kind, string? PointerPath, DateTimeOffset TakenAt,
    string Verdict, string? VerdictBy, DateTimeOffset? VerdictAt, string? VerdictNote, bool? Landed);
