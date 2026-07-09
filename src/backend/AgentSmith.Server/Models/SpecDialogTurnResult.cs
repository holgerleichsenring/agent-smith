using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// p0315e: what one design-conversation turn produced — the reply text the
/// router delivers threaded, and the typed terminal outcome the outcome flow
/// proposes for confirmation (AnswerOutcome ends the turn with no artifact).
/// </summary>
public sealed record SpecDialogTurnResult(string Reply, OutcomeProposal Outcome);
