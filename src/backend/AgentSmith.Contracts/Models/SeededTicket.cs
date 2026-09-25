namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-25-8e51c: the ticket a bound design conversation is grounded on, as it travels from
/// the server onto the turn and into the prompt.
/// <para>
/// In Contracts because the turn SEEDS it in the server and the prompt factory RENDERS it in the
/// application layer, and a context key carrying a type only one of them can name is a key the
/// other has to reconstruct.
/// </para>
/// </summary>
/// <param name="Truncated">True when the seeding cap dropped part of the ticket. The turn says so,
/// because a short answer must never be mistaken for a short ticket.</param>
/// <param name="Moved">True when the ticket's text has changed since this conversation read it —
/// computed from the fingerprint, never asked of the operator.</param>
/// <param name="Divergence">2026-09-25-8e51e: where this ticket and the specification somebody
/// approved for it disagree, computed before the turn runs. Null when there is no approved set or
/// the two already say the same thing.</param>
public sealed record SeededTicket(
    string Title, string Text, bool Truncated, string Fingerprint, bool Moved = false,
    SetDivergence? Divergence = null);
