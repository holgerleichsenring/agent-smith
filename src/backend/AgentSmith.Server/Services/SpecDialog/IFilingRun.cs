using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-17-5701b: files an approved proposal through the 'file' pipeline and answers with what the filing did.
/// The approval sink and the closed ticket's follow-up are its callers; the later acts on a filed ticket (amend,
/// close, reopen, dependents' start) become pipelines beside it.
/// </summary>
public interface IFilingRun
{
    /// <param name="proposalId">The proposal the approval decided; its approve turn is left out of the owner words.</param>
    Task<FilingReport> RunAsync(ConversationState state, OutcomeProposal proposal, bool mayStartRuns, string? proposalId = null);
}
