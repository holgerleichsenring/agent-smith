namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-18-7a05: the durable half of deleting a design conversation — the session row and
/// the answers stored against its id, in one unit of work.
/// <para>
/// It is an interface because the route holds the turn gate ACROSS this call and releases it in
/// a finally. Proving that the release survives a failure needs a failure that happens AFTER
/// the hold is taken, and database work written inline in the route would leave nowhere to put
/// one.
/// </para>
/// </summary>
public interface ISpecDialogConversationDeleter
{
    Task DeleteAsync(string sessionId, CancellationToken cancellationToken);
}
