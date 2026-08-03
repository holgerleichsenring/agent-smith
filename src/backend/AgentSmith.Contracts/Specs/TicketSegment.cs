namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: one addressable block of the ticket text, cut deterministically in
/// code before the model ever sees it. The segment id is the ANCHOR vocabulary:
/// the derivation returns ids, never content, because a model that retypes a code
/// template produces a plausible copy — and a plausible copy of a naming contract
/// is the failure mode this whole line of work started from.
/// </summary>
public sealed record TicketSegment(int Id, string Text, int FirstLine, int LastLine);
