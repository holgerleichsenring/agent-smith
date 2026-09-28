namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// 2026-09-27-481ba: reading the ticket a design conversation is bound to, in slices.
/// <para>
/// The seeded text is capped, and until now the prompt told the model to SAY SO if the answer
/// depended on the rest — an instruction to report a limitation with no way to overcome it, which
/// is a dead end exactly when the answer does depend on the rest.
/// </para>
/// <para>
/// IT RE-READS THE TRACKER, because "the rest" is stored nowhere: the ticket is read once when the
/// conversation comes into existence, and what is kept is the already-capped text in a column
/// bounded at the same number. So this answers the ticket AS IT STANDS NOW, which is not the same
/// as what the cap dropped — it is the more useful of the two, and the difference is real: the
/// fingerprint that decides whether a ticket has "moved" covers title, description and acceptance
/// criteria only, so a comment posted after the read never marks it moved and would surface here
/// although it was never dropped.
/// </para>
/// </summary>
public interface ITicketReader
{
    /// <summary>
    /// A slice of the ticket from <paramref name="from"/>, and what remains after it. Never throws:
    /// a tracker that cannot be reached is an answer carrying the reason.
    /// </summary>
    Task<TicketReadSlice> ReadAsync(int from, CancellationToken cancellationToken);
}

/// <summary>What was read, where it started, what is left, and why nothing came back.</summary>
public sealed record TicketReadSlice(string Text, int From, int Remaining, string? Reason)
{
    public static TicketReadSlice Failed(string reason) => new(string.Empty, 0, 0, reason);
}
