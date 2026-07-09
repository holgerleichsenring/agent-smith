namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: resolves a design-partner reply's terminal output into a typed
/// OutcomeProposal BEFORE the reply is shown. Plain prose = answer (today's
/// default path); one bare ```yaml block = phase (the p0315b draft contract,
/// unchanged); one ```outcome block = bug or epic. An invalid outcome
/// re-prompts the master, never surfaces raw.
/// </summary>
public interface IOutcomeProposalResolver
{
    OutcomeResolution Resolve(string reply);
}
