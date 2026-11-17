using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-17-5701b: files an approved proposal through a 'file' run — the server accepts the approval and starts the
/// run; the run's steps create, record, write and start, under a run id. It runs on no token: the approval is already
/// claimed, and a filing a caller's cancellation broke off would leave a created root nobody starts. The answer is the
/// run's report, or — when the run ended before it filed anything — a report carrying the run's failure.
/// 2026-11-17-e061a: launched through <see cref="IActRun"/>, the entry every act's run shares.
/// </summary>
public sealed class FilingRun(IActRun runs) : IFilingRun
{
    public async Task<FilingReport> RunAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns, string? proposalId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var project = state.Scope?.Project ?? state.Project;
        if (string.IsNullOrWhiteSpace(project))
            return new FilingReport([], "The conversation has no active-scope project to file tickets into.");
        var slot = new FilingReportSlot();
        var result = await runs.RunAsync(project, PipelinePresets.FileName, new Dictionary<string, object>
        {
            [ContextKeys.FilingRequest] = ConversationApproval.Request(state, proposal, mayStartRuns, proposalId),
            [ContextKeys.FilingReport] = slot,
        });
        return slot.Report ?? new FilingReport([], result.Message ?? "The filing run ended before it filed anything.");
    }
}
