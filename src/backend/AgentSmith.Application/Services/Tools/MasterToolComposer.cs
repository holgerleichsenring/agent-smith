using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0280: the surface one master pass runs on — its base surface (read-only Review for a
/// scan master, read/write for a coding master) plus spawn_agents and
/// read_sub_agent_observations when sub-agents are enabled. Children SHARE the master's
/// filesystem host, so their reads and writes aggregate into the master's read-set and
/// changes, and they get the same base surface — never spawn_agents.
/// <para>
/// p0315b: the spec-dialog surface is content-reads and ask_human only — a conversation turn
/// neither writes nor delegates.
/// </para>
/// <para>
/// 2026-08-30-3c12: extracted from AgenticMasterHandler, whose one reason to change is the
/// master PASS — driving it, re-driving it, and publishing what it produced. Which tools
/// that pass is given is a second reason, and it is the one that keeps changing.
/// </para>
/// </summary>
public sealed class MasterToolComposer(
    AgenticToolSurface toolSurface,
    ScanStationToolFactory stationTools, // 2026-08-30-18e3: record_entry_station
    ScanRequirementToolFactory requirementTools, // 2026-08-30-3c12: the standard's entries
    EnsureRepoSandboxToolFactory ensureRepoSandboxFactory, // p0331
    ISubAgentRunner subAgentRunner,
    SubAgentBudget subAgentBudget,
    SubAgentNameValidator subAgentNameValidator,
    IChildAnswerStore childAnswerStore,
    IDecisionLogger decisionLogger,
    LoopLimitsConfig loopLimits,
    ILogger<MasterToolComposer> logger)
{
    public IList<AITool> Compose(
        bool isScanMaster, bool isSpecDialog, FilesystemToolHost fs, LogDecisionToolHost log,
        IToolHost human, GetArtifactCredentialsToolHost credentials,
        WriteContextYamlToolHost writeContextYaml, WebToolHost? web, ProgressLedgerToolHost progress,
        MemoryRecallToolHost recall, MemoryWriteToolHost remember, AgenticMasterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (isSpecDialog) return toolSurface.SpecDialog(fs, human, web, recall, remember);
        IList<AITool> BaseSurface() => Base(isScanMaster, fs, log, human, credentials,
            writeContextYaml, web, recall, remember, context);

        var master = BaseSurface();
        // p0331: coding masters get the ensure_repo_sandbox escalation valve — the
        // counterpart to ScopeRepos' conservative narrowing. Scan masters read everything
        // anyway (full scope, no narrowing) and must not spawn.
        // p0341: coding masters also get update_progress (the durable ledger); scan /
        // spec-dialog surfaces never do — a read-only review keeps no checklist.
        if (!isScanMaster)
            master = master
                .Concat(ensureRepoSandboxFactory.Create(context.Pipeline, fs, logger).GetTools(null, null))
                .Concat(progress.GetTools(null, null))
                .ToList();
        return loopLimits.MaxSubAgentsPerRun <= 0 ? master : WithSubAgents(master, BaseSurface, context);
    }

    /// <summary>
    /// p0380: recall (read) and remember (memory-only proposal) join EVERY master surface,
    /// including the read-only scan surface. 2026-08-30-18e3 / 3c12: the entry-map and
    /// requirement tools ride on the scan surface for the ONE master asked to state a map —
    /// the factories yield nothing for the other two, so the shared Review surface stays
    /// exactly what it is for them.
    /// </summary>
    private IList<AITool> Base(
        bool isScanMaster, FilesystemToolHost fs, LogDecisionToolHost log, IToolHost human,
        GetArtifactCredentialsToolHost credentials, WriteContextYamlToolHost writeContextYaml,
        WebToolHost? web, MemoryRecallToolHost recall, MemoryWriteToolHost remember,
        AgenticMasterContext context) =>
        isScanMaster
            ? [.. toolSurface.Review(fs, log, web, recall, remember),
                .. stationTools.For(context.MasterSkillName, context.Pipeline),
                .. requirementTools.For(context.MasterSkillName, context.Pipeline)]
            : toolSurface.ReadWriteWithHuman(
                fs, log, human, web: web, credentials: credentials, writeContextYaml: writeContextYaml,
                recall: recall, remember: remember);

    /// <summary>
    /// The fan-out surface. A scan master keeps it too: it is exempted from the configured
    /// master ceiling and falls to the per-request one, so one worker per entry group is the
    /// only place the volume this asks for can be done at all.
    /// </summary>
    private IList<AITool> WithSubAgents(
        IList<AITool> master, Func<IList<AITool>> baseSurface, AgenticMasterContext context)
    {
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var rid) && rid is not null
            ? rid : "run";
        var sandboxes = context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var subCtx = new SubAgentContext(
            context.Pipeline, sandboxes, PipelineCostTracker.GetOrCreate(context.Pipeline), runId,
            ChildTools: baseSurface().ToList(), AnswerStore: childAnswerStore, Budget: subAgentBudget,
            AgentConfig: context.AgentConfig);
        var spawn = new SpawnAgentToolHost(
            subAgentRunner, subAgentBudget, subAgentNameValidator, decisionLogger, subCtx);
        var readObs = new ReadSubAgentObservationsToolHost(childAnswerStore);
        return master.Concat(spawn.GetTools(null, null)).Concat(readObs.GetTools(null, null)).ToList();
    }
}
