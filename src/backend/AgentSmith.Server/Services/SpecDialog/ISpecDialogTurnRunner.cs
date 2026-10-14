using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: runs one design-conversation turn for an open spec-dialog session
/// and returns the reply text plus its typed terminal outcome (p0315e). The
/// router persists and delivers the reply, then hands the outcome to the
/// outcome flow. The poster is who asked for the turn and what they may do — the bound ports act as them.
/// </summary>
public interface ISpecDialogTurnRunner
{
    Task<SpecDialogTurnResult> RunTurnAsync(ConversationState state, TurnPoster poster, CancellationToken cancellationToken);
}
