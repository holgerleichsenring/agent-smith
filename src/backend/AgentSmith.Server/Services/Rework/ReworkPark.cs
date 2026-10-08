namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-10-08-e8b9b: whether the ticket can be reworked (Parked: parked after a finished run,
/// or already claimable), and the trigger status to move to (null: no move —
/// the status already is one, or the trigger names none and accepts every status).</summary>
public sealed record ReworkPark(bool Parked, string? MoveTo);
