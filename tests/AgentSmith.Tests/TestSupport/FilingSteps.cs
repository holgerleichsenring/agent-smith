using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-17-5701b: the 'file' run's two steps — the REAL PlanFiling and FileTickets handlers over the real filing
/// services — run in order on a context seeded the way <see cref="FilingRun"/> seeds it, without the run lifecycle.
/// What the filing suites drive in place of the retired filers; <see cref="FilingDoubles"/> composes it.
/// </summary>
internal sealed class FilingSteps(AgentSmithConfig config, PlanFilingHandler plan, FileTicketsHandler file) : IFilingRun
{
    /// <summary>What the FileTickets step of the last filing answered; null when PlanFiling ended it.</summary>
    public AgentSmith.Domain.Models.CommandResult? Filed { get; private set; }

    public Task<FilingReport> RunAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns, string? proposalId = null) =>
        FileAsync(state, proposal, mayStartRuns, CancellationToken.None, proposalId);

    /// <summary>A run's answer: the slot's report, or the failure of the step that ended it before any report.</summary>
    public async Task<FilingReport> FileAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns, CancellationToken ct, string? proposalId = null)
    {
        var name = state.Scope?.Project ?? state.Project;
        if (LiveConfig.Projects(config).Of(name) is not { } project)
            return new FilingReport([], $"Project '{name}' not found in configuration.");
        var (pipeline, slot) = Seeded(ConversationApproval.Request(state, proposal, mayStartRuns, proposalId), project);
        var planned = await plan.ExecuteAsync(new PlanFilingContext(pipeline, project), ct);
        Filed = null;
        if (!planned.IsSuccess) return new FilingReport([], planned.Message);
        Filed = await file.ExecuteAsync(new FileTicketsContext(pipeline, project), ct);
        return slot.Report ?? new FilingReport([], Filed.Message);
    }

    private static (PipelineContext Pipeline, FilingReportSlot Slot) Seeded(FilingRequest request, ResolvedProject project)
    {
        var (pipeline, slot) = (new PipelineContext(), new FilingReportSlot());
        pipeline.Set(ContextKeys.FilingRequest, request);
        pipeline.Set(ContextKeys.FilingReport, slot);
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, project.Repos);
        return (pipeline, slot);
    }
}
