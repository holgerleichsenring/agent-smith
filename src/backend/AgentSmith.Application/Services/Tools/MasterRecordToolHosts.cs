using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
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
    ILogger<MasterRecordToolHosts> logger,
    MemorySources? sources = null, // 2026-11-10-fcd7b
    Specs.Revision.MasterSpecRevision? revision = null) // 2026-11-17-ec48a
{
    /// <param name="codingMaster">2026-11-10-4778 review: only a coding master's remember may keep a lesson on the
    /// project ('@project'); a scan or review master's writes only where a code change lands.</param>
    /// <param name="agent">2026-11-17-ec48a: the master's agent, which a spec revision's review is made under.</param>
    public MasterRecordTools Create(PipelineContext pipeline, string repoRoot, bool codingMaster = false, AgentConfig? agent = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var repositories = runRepositories.For(pipeline);
        var spec = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft : null;
        var defaults = Recorder(pipeline, spec?.SpecId);
        var memory = new MasterMemoryUse(pipeline, repositories, repoRoot, citations, MemoryUse(pipeline, spec?.SpecId));
        return new MasterRecordTools(
            new LogDecisionToolHost(decisionLogger, repositories),
            new MemoryRecallToolHost(repositories, repoRoot, standings, pipeline, logger, sources),
            new MemoryWriteToolHost(repositories, repoRoot, logger, codingMaster ? sources : null, pipeline),
            spec is null ? null : new HandForwardToolHost(pipeline, spec.SpecId, memory),
            decisionLogger,
            defaults is null ? null : new DefaultDecisionToolHost(defaults),
            memory.CanRecord ? new MemoryDisputeToolHost(memory) : null,
            sources is null ? null : new ProjectMemoryWriteToolHost(sources, pipeline))
        {
            ReviseSpec = spec is not null && codingMaster && agent is not null && revision is not null
                ? new Specs.Revision.ReviseSpecToolHost(revision, pipeline, agent) : null,
            FinishSpec = spec is not null && codingMaster ? new FinishSpecToolHost() : null, // 2026-11-18-98fa
        };
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
/// DefaultDecision and Dispute are null where the pipeline holds no run id. 2026-11-10-fcd7b: ProjectRemember is the
/// design turn's remember, which <see cref="DesignWorkToolGate"/> offers only to a poster who may start runs.
/// </summary>
public sealed record MasterRecordTools(
    LogDecisionToolHost Log,
    MemoryRecallToolHost Recall,
    MemoryWriteToolHost Remember,
    HandForwardToolHost? HandForward,
    IDecisionLogger Decisions,
    DefaultDecisionToolHost? DefaultDecision,
    MemoryDisputeToolHost? Dispute = null,
    ProjectMemoryWriteToolHost? ProjectRemember = null)
{
    /// <summary>2026-11-17-ec48a: revise_spec, beside hand_forward on a coding master that executes a spec only.</summary>
    public Specs.Revision.ReviseSpecToolHost? ReviseSpec { get; init; }

    /// <summary>2026-11-18-98fa: finish_spec, beside hand_forward on a coding master that executes a spec only.</summary>
    public FinishSpecToolHost? FinishSpec { get; init; }

    /// <summary>The tools a spec's coding master gets beyond the base surface: hand_forward, revise_spec, finish_spec.</summary>
    public IEnumerable<Microsoft.Extensions.AI.AITool> SpecTools =>
        [.. HandForward?.GetTools(null, null) ?? [], .. ReviseSpec?.GetTools(null, null) ?? [],
         .. FinishSpec?.GetTools(null, null) ?? []];
}
