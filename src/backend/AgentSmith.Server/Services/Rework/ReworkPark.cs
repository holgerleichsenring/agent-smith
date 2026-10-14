namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-10-08-e8b9b: whether the ticket can be reworked (Parked: parked after a finished run,
/// or already claimable), and the trigger status to move to (null: no move —
/// the status already is one, or the trigger names none and accepts every status).
/// <para>2026-10-13-9a59t: Closed — a person closed the ticket for work (TicketClosure); an act on it files
/// a linked follow-up instead of reopening it.</para></summary>
public sealed record ReworkPark(bool Parked, string? MoveTo, bool Closed = false);
