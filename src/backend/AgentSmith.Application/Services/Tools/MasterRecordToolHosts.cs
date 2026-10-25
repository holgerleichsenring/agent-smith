using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-13-8762m: the record tools of one master start — log_decision, recall and remember, backed per
/// REPOSITORY and resolved on every call so a repository ensure_repo_sandbox adds mid-run counts, and
/// hand_forward when the master executes a spec. 2026-10-13-8762o: and decide_by_default where the pipeline
/// holds a run id. 2026-10-25-9324b: and dispute_memory
/// beside them, with hand_forward's relied_on resolved the same way. 2026-10-25-6c1f: recall reads the standing.
/// </summary>
public sealed class MasterRecordToolHosts(
    IDecisionLogger decisionLogger,
    RunRepositoriesFactory runRepositories,
    IEventPublisher events,
    MemoryCitationResolver citations,
    MemoryStandingReader standings,
    ILogger<MasterRecordToolHosts> logger)
{
    public MasterRecordTools Create(PipelineContext pipeline, string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var repositories = runRepositories.For(pipeline);
        var spec = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft : null;
        var defaults = Recorder(pipeline, spec?.SpecId);
        var memory = new MasterMemoryUse(pipeline, repositories, repoRoot, citations, MemoryUse(pipeline, spec?.SpecId));
        return new MasterRecordTools(
            new LogDecisionToolHost(decisionLogger, repositories),
            new MemoryRecallToolHost(repositories, repoRoot, standings, pipeline, logger),
            new MemoryWriteToolHost(repositories, repoRoot, logger),
            spec is null ? null : new HandForwardToolHost(pipeline, spec.SpecId, memory),
            decisionLogger,
            defaults is null ? null : new DefaultDecisionToolHost(defaults),
            memory.CanRecord ? new MemoryDisputeToolHost(memory) : null);
    }

    private MemoryUseRecorder? MemoryUse(PipelineContext pipeline, string? specId)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return null;
        var project = pipeline.TryGet<string>(ContextKeys.ProjectName, out var name) ? name ?? string.Empty : string.Empty;
        var ticket = pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var id) ? id?.Value ?? string.Empty : string.Empty;
        return new MemoryUseRecorder(events, runId, specId, project, ticket, logger);
    }

    private DefaultDecisionRecorder? Recorder(PipelineContext pipeline, string? specId)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return null;
        var conversation = pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var job) && !string.IsNullOrEmpty(job)
            ? job : null;
        return new DefaultDecisionRecorder(events, runId, specId, conversation, logger);
    }
}

/// <summary>
/// 2026-10-13-8762m: what <see cref="MasterRecordToolHosts"/> builds for one master start. HandForward is
/// null unless the master executes a spec; Decisions is the logger the open loop records its stop with;
/// DefaultDecision and Dispute are null where the pipeline holds no run id.
/// </summary>
public sealed record MasterRecordTools(
    LogDecisionToolHost Log,
    MemoryRecallToolHost Recall,
    MemoryWriteToolHost Remember,
    HandForwardToolHost? HandForward,
    IDecisionLogger Decisions,
    DefaultDecisionToolHost? DefaultDecision,
    MemoryDisputeToolHost? Dispute = null);
