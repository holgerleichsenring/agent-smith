namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-09-25-8e51c: how much of a ticket a design conversation carries into every turn.
/// <para>
/// In Contracts because TWO layers need the same number and must not each pick one: the composer
/// that enforces it, and the column that stores what the composer produced. A column shorter than
/// the cap truncates silently; a cap larger than the column fails the write.
/// </para>
/// <para>
/// A ceiling at all because a conversation re-sends its whole prompt every turn, on the one
/// surface with neither a cost fence nor an iteration ceiling — whatever is seeded is multiplied
/// by the length of the conversation rather than paid once. One live ticket thread ran to
/// 147,462 characters. This is the number the run path's own ticket sections already cap at.
/// </para>
/// </summary>
public static class SeededTicketLimits
{
    public const int Text = 20_000;
}
