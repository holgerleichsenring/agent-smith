using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: runs one design-conversation turn for an open spec-dialog session
/// and returns the reply text. The router persists and delivers the reply.
/// </summary>
public interface ISpecDialogTurnRunner
{
    Task<string> RunTurnAsync(ConversationState state, CancellationToken cancellationToken);
}
