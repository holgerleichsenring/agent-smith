using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Decides which tool surface a master gets and whether the sub-agent pair joins it:
/// the base surface (read-only Review for a scan master, content-reads for a design
/// turn, read/write for a coding master), the escalation valve and the progress ledger
/// where they belong, and the spawn / read-observations pair under the fan-out count
/// that turn gates on.
/// </summary>
public sealed class MasterToolComposition(
    AgenticToolSurface toolSurface,
    EnsureRepoSandboxToolFactory ensureRepoSandboxFactory,
    LoopLimitsConfig loopLimits,
    SubAgentBudget subAgentBudget,
    ISubAgentRunner subAgentRunner,
    SubAgentNameValidator subAgentNameValidator,
    IDecisionLogger decisionLogger,
    IChildAnswerStore childAnswerStore,
    ILogger<MasterToolComposition> logger)
{
    // p0280: the master surface = its base surface (read-only Review for a scan master,
    // read/write for a coding master) PLUS spawn_agents + read_sub_agent_observations when
    // sub-agents are enabled. Children SHARE this fs (so their reads/writes aggregate into
    // the master's read-set + changes) and get the same base surface — never spawn_agents.
    // p0315b: the spec-dialog surface is content-reads + ask_human only.
    // 2026-09-22-5891: a design turn fans out too, under its OWN count and child ceiling, and
    // is excluded BY NAME below from the escalation valve (which would spawn a WRITABLE
    // sandbox into a read-only turn) and the ledger.
    public IList<AITool> Compose(
        bool isScanMaster, bool isSpecDialog, FilesystemToolHost fs, LogDecisionToolHost log, IToolHost human,
        GetArtifactCredentialsToolHost credentials, WriteContextYamlToolHost writeContextYaml,
        WebToolHost? web, ProgressLedgerToolHost progress,
        MemoryRecallToolHost recall, MemoryWriteToolHost remember,
        WithdrawFiledTicketToolHost? withdraw, AgenticMasterContext context)
    {
        // p0380: recall (read) + remember (memory-only proposal) join EVERY
        // master surface, including the read-only Review/scan surface.
        // 2026-09-22-9519: the withdrawal joins the design surface HERE rather than inside
        // AgenticToolSurface — only a design turn with a dialogue identity has one, and that is
        // what this method decides.
        IList<AITool> BaseSurface() => isSpecDialog
            ? [.. toolSurface.SpecDialog(fs, human, web, recall, remember),
               .. withdraw?.GetTools(null, null) ?? []]
            : isScanMaster
                ? toolSurface.Review(fs, log, web, recall, remember)
                : toolSurface.ReadWriteWithHuman(
                    fs, log, human, web: web, credentials: credentials, writeContextYaml: writeContextYaml,
                    recall: recall, remember: remember);

        var master = BaseSurface();
        // p0331: coding masters get the ensure_repo_sandbox escalation valve — the
        // counterpart to ScopeRepos' conservative narrowing. Scan masters read
        // everything anyway (full scope, no narrowing) and must not spawn.
        // p0341: coding masters also get update_progress (the durable ledger); scan /
        // spec-dialog surfaces never do — a read-only review keeps no checklist.
        if (!isScanMaster && !isSpecDialog)
            master = master
                .Concat(ensureRepoSandboxFactory.Create(context.Pipeline, fs, logger).GetTools(null, null))
                .Concat(progress.GetTools(null, null))
                .ToList();

        // A design turn gates on ITS OWN count, so it fans out with the run-wide number at 0.
        var fanOut = isSpecDialog ? loopLimits.MaxSubAgentsPerDialogTurn : loopLimits.MaxSubAgentsPerRun;
        if (fanOut <= 0) return master;

        // The injected budget's capacity is frozen from the run-wide number, so a turn that
        // only READ its own count would still be granted twenty. It builds the budget it gates
        // on and hands that one to the spawn host, which reserves from it.
        var budget = isSpecDialog ? new SubAgentBudget(fanOut) : subAgentBudget;
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var rid) && rid is not null ? rid : "run";
        var sandboxes = context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var subCtx = new SubAgentContext(
            context.Pipeline, sandboxes, PipelineCostTracker.GetOrCreate(context.Pipeline), runId,
            ChildTools: [.. isSpecDialog ? SpecDialogChildTools.Of(BaseSurface()) : BaseSurface()],
            AnswerStore: childAnswerStore, Budget: budget, AgentConfig: context.AgentConfig,
            // No governor hooks on a child: how far it may go bounds the whole wave.
            ChildIterationCeiling: isSpecDialog ? loopLimits.MaxDialogSubAgentLoopIterations : null);
        var spawn = new SpawnAgentToolHost(subAgentRunner, budget, subAgentNameValidator, decisionLogger, subCtx);
        var readObs = new ReadSubAgentObservationsToolHost(childAnswerStore);
        return master.Concat(spawn.GetTools(null, null)).Concat(readObs.GetTools(null, null)).ToList();
    }
}
