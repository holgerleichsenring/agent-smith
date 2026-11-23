namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcj: one revision of a series ticket's spec set after its first — a spec appended, recut or
/// dropped — as its ticket branch's revision history records it.</summary>
/// <param name="TicketId">The ticket whose set changed.</param>
/// <param name="Revision">The revision's number.</param>
/// <param name="Cause">What the revision names as its cause.</param>
/// <param name="At">When the revision was written.</param>
public sealed record SeriesRunChangeView(string TicketId, int Revision, string Cause, DateTimeOffset At);
