namespace AgentSmith.Contracts.Series;

/// <summary>
/// 2026-11-27-3532b: the series master's tell_ticket — the text for a member ticket, answered by its key. A ticket whose run
/// is parked on a question gets it as that question's answer; with <paramref name="QuestionId"/> only that question, so an
/// answer never lands on a question the run asked since.
/// </summary>
/// <param name="QuestionId">The checkpoint question the text answers; null for whatever the run is parked on.</param>
public sealed record SeriesTell(
    string Project, string TicketId, string TicketKey, string Text, string Principal, bool MayStartRuns, string? QuestionId);
