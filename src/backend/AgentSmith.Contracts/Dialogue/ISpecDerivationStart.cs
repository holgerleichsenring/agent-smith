namespace AgentSmith.Contracts.Dialogue;

/// <summary>
/// 2026-10-26-bdd0g: deriving the specs of the ticket a conversation is bound to, as the person who posted the turn.
/// Built only for a poster who may start runs. A derivation starts no attempt and never moves the ticket.
/// </summary>
public interface ISpecDerivationStart
{
    /// <summary>The sentence the conversation is told: started, or why not.</summary>
    /// <param name="preview">True to write nothing to the branch; false to store the specs on the ticket branch.</param>
    /// <param name="reason">What the person asked for — kept in the log beside the request.</param>
    Task<string> StartAsync(bool preview, string reason, CancellationToken cancellationToken);
}
