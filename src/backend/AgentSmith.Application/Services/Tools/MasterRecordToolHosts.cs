using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-13-8762m: the record tools of one master start — log_decision, recall and remember, backed per
/// REPOSITORY and resolved on every call so a repository ensure_repo_sandbox adds mid-run counts, and
/// hand_forward when the master executes a spec.
/// </summary>
public sealed class MasterRecordToolHosts(
    IDecisionLogger decisionLogger,
    RunRepositoriesFactory runRepositories,
    ILogger<MasterRecordToolHosts> logger)
{
    public MasterRecordTools Create(PipelineContext pipeline, string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var repositories = runRepositories.For(pipeline);
        var spec = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft : null;
        return new MasterRecordTools(
            new LogDecisionToolHost(decisionLogger, repositories),
            new MemoryRecallToolHost(repositories, repoRoot, logger),
            new MemoryWriteToolHost(repositories, repoRoot, logger),
            spec is null ? null : new HandForwardToolHost(pipeline, spec.SpecId),
            decisionLogger);
    }
}

/// <summary>
/// 2026-10-13-8762m: what <see cref="MasterRecordToolHosts"/> builds for one master start. HandForward is
/// null unless the master executes a spec; Decisions is the logger the open loop records its stop with.
/// </summary>
public sealed record MasterRecordTools(
    LogDecisionToolHost Log,
    MemoryRecallToolHost Recall,
    MemoryWriteToolHost Remember,
    HandForwardToolHost? HandForward,
    IDecisionLogger Decisions);
