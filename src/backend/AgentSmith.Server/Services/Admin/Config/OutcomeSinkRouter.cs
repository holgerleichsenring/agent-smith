using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Admin.Config;

/// <summary>2026-11-17-f3a8c: an approval goes where its outcome is acted on — configuration changes to a config run,
/// everything else to the filing.</summary>
public sealed class OutcomeSinkRouter(ConfigOutcomeSink config, TicketFilingOutcomeSink filing) : IOutcomeSink
{
    public Task<OutcomeAcceptance> AcceptAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
        CancellationToken cancellationToken, string? proposalId = null) =>
        proposal is ConfigChangeOutcome
            ? config.AcceptAsync(state, proposal, mayStartRuns, cancellationToken, proposalId)
            : filing.AcceptAsync(state, proposal, mayStartRuns, cancellationToken, proposalId);
}
