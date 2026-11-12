using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
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
/// where they belong, and spawn_agents under the fan-out count that turn gates on
/// (2026-11-10-14a3a: answers come back whole, so nothing reads them a second time). Every function on the master's and the children's surface carries the
/// sanity annotations.
/// </summary>
public sealed class MasterToolComposition(
    AgenticToolSurface toolSurface,
    EnsureRepoSandboxToolFactory ensureRepoSandboxFactory,
    LoopLimitsConfig loopLimits,
    SubAgentBudget subAgentBudget,
    ISubAgentRunner subAgentRunner,
    SubAgentNameValidator subAgentNameValidator,
    IDecisionLogger decisionLogger,
    DesignReadToolHostFactory designRead, // 2026-10-01-7f7ab
    RenderReferenceToolFactory renderReference, // 2026-10-01-283de
    TicketActToolsFactory ticketActs, DesignWorkToolGate designWork, // 2026-11-10-fcd7b
    TurnActivityTools reportingTools,
    GateStageToolHost gateStage,
    Loop.Sanity.ToolResultSanityWrapper sanity,
    ILogger<MasterToolComposition> logger)
{
    // Children share the master's filesystem and base surface, never spawn_agents. A design turn fans out under its
    // own count and child ceiling and is kept off the escalation valve (a writable sandbox) and the ledger.
    public IList<AITool> Compose(
        bool isScanMaster, bool isSpecDialog, FilesystemToolHost fs, MasterRecordTools records, IToolHost human,
        GetArtifactCredentialsToolHost credentials, WriteContextYamlToolHost writeContextYaml,
        WebToolHost? web, ProgressLedgerToolHost progress,
        WithdrawFiledTicketToolHost? withdraw, ReadTicketToolHost? readTicket,
        TicketWorkToolHost? ticketWork, TicketRunsToolHost? ticketRuns,
        AgenticMasterContext context)
    {
        Loop.Sanity.RefusedWriteLog.OpenFresh(context.Pipeline); // 2026-10-17-a196a: this step's refusals only
        // p0380: recall (read) + remember (memory-only proposal) join EVERY master surface, including Review/scan.
        // 2026-09-22-9519: the withdrawal joins the design surface here, built only where a dialogue identity exists.
        // 2026-11-10-14a3d: a design turn's memory tools only over a store its grounding found.
        var memory = !isSpecDialog || context.Pipeline.TryGet<string>(ContextKeys.MemoryIndex, out var index) && !string.IsNullOrWhiteSpace(index);
        IList<AITool> BaseSurface() => isSpecDialog
            ? [.. toolSurface.SpecDialog(fs, web, memory ? records.Recall : null),
               .. withdraw?.GetTools(null, null) ?? [],
               // 2026-09-27-481ba: and reading the ticket the turn was seeded with, on the same
               // gate — only a BOUND design turn carries the port this is built from.
               .. readTicket?.GetTools(null, null) ?? [],
               .. ticketWork?.GetTools(null, null) ?? [],
               .. ticketRuns?.GetTools(null, null) ?? []]
            : isScanMaster
                ? toolSurface.Review(fs, records.Log, web, records.Recall, records.Remember)
                : toolSurface.ReadWriteWithHuman(
                    fs, records.Log, human, web: web, credentials: credentials, writeContextYaml: writeContextYaml,
                    recall: records.Recall, remember: records.Remember);

        var master = BaseSurface();
        // 8762o: decide_by_default — the coding master only, no child; fc66: a design turn's choices go in its reply.
        if (!isScanMaster && !isSpecDialog) master = [.. master, .. records.DefaultDecision?.GetTools(null, null) ?? []];
        if (memory) master = [.. master, .. records.Dispute?.GetTools(null, null) ?? []]; // 9324b: every master, no child
        // 2026-10-01-7f7ab: design_read joins design and coding masters of a project with a figma
        // source — the MASTER only, outside BaseSurface: children fanning out over one file would
        // multiply calls against one rate limit, so no child surface derived from it carries it.
        if (!isScanMaster && designRead.Create(context.Pipeline) is { } design)
            master = [.. master, .. design.GetTools(null, null)];
        // 2026-10-01-283de: render_reference joins the design MASTER — one browser sandbox serves the
        // conversation, so children fanning out would only queue on it. 2026-10-01-283df: and the
        // coding master of a project whose config enables the browser; the factory decides.
        // 2026-10-01-283di: with compare_reference beside it.
        if (!isScanMaster) master = [.. master, .. renderReference.Tools(context.Pipeline, isSpecDialog)];
        // The bound-ticket acts (start_next_attempt, the series tools) join the design MASTER outside BaseSurface:
        // a child acts on no one's word, so no child surface carries them.
        if (isSpecDialog) master = [.. master, .. ticketActs.Tools(context), .. designWork.Tools(context.Pipeline, records),
            .. ConversationNotesToolHost.For(context.Pipeline)?.GetTools(null, null) ?? []]; // 2026-11-12-cd4ck
        if (context.IsTicketSteward) master = TicketStewardTools.Of(master); // answers and acts, files nothing
        // p0331: coding masters get the ensure_repo_sandbox escalation valve — the
        // counterpart to ScopeRepos' conservative narrowing. Scan masters read
        // everything anyway (full scope, no narrowing) and must not spawn.
        // p0341: coding masters also get update_progress (the durable ledger); scan /
        // spec-dialog surfaces never do — a read-only review keeps no checklist.
        if (!isScanMaster && !isSpecDialog)
            master = master
                .Concat(ensureRepoSandboxFactory.Create(context.Pipeline, fs, logger).GetTools(null, null))
                .Concat(progress.GetTools(null, null))
                .Concat(gateStage.Tools(context.Pipeline))
                .Concat(records.HandForward?.GetTools(null, null) ?? []) // 8762m: a spec's master only, never a child
                .ToList();
        // A design turn gates on ITS OWN count, so it fans out with the run-wide number at 0.
        var fanOut = isSpecDialog ? loopLimits.MaxSubAgentsPerDialogTurn : loopLimits.MaxSubAgentsPerRun;
        if (fanOut <= 0) return sanity.Wrap(master, context.Pipeline);

        // The injected budget's capacity is frozen from the run-wide number, so a turn that
        // only READ its own count would still be granted twenty. It builds the budget it gates
        // on and hands that one to the spawn host, which reserves from it.
        var budget = isSpecDialog ? new SubAgentBudget(fanOut) : subAgentBudget;
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var rid) && rid is not null ? rid : "run";
        var sandboxes = context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var subCtx = new SubAgentContext(
            context.Pipeline, sandboxes, PipelineCostTracker.GetOrCreate(context.Pipeline), runId,
            ChildTools: sanity.Wrap(isSpecDialog ? reportingTools.Reporting(SpecDialogChildTools.Of(BaseSurface())) : BaseSurface(), context.Pipeline),
            Budget: budget, AgentConfig: context.AgentConfig,
            // No governor hooks on a child: how far it may go bounds the whole wave.
            ChildIterationCeiling: isSpecDialog ? loopLimits.MaxDialogSubAgentLoopIterations : null);
        var spawn = new SpawnAgentToolHost(subAgentRunner, budget, subAgentNameValidator, decisionLogger, subCtx);
        return sanity.Wrap(master.Concat(spawn.GetTools(null, null)), context.Pipeline);
    }
}
