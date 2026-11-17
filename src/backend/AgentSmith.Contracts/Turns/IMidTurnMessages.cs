namespace AgentSmith.Contracts.Turns;

/// <summary>
/// 2026-11-12-a1fa: the messages the operator sent while one design turn runs, for that turn's master to read with its
/// next tool result. One instance belongs to one turn and knows the turn's poster. 2026-11-17-4860b: or to one code run
/// master, handed what the ticket's conversation addressed to that run.
/// </summary>
public interface IMidTurnMessages
{
    /// <summary>Says which master leads the turn; a turn the ticket steward leads keeps the turn-in-progress reply.</summary>
    void Lead(bool stewarded);

    /// <summary>Takes the waiting messages of the turn's poster, oldest first; each is handed out once. 2026-11-17-4860b:
    /// asynchronous, because a code run reads its messages from the ticket conversation's stored transcript.</summary>
    ValueTask<IReadOnlyList<MidTurnMessage>> DrainAsync(CancellationToken cancellationToken);
}
