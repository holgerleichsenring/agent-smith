using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: runs one design-conversation turn for an open spec-dialog session
/// and returns the reply text plus its typed terminal outcome (p0315e). The
/// router persists and delivers the reply, then hands the outcome to the
/// outcome flow.
/// </summary>
public interface ISpecDialogTurnRunner
{
    Task<SpecDialogTurnResult> RunTurnAsync(ConversationState state, CancellationToken cancellationToken);
}
