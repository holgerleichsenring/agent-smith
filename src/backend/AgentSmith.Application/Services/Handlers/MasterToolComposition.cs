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
public sealed partial class MasterToolComposition(
    AgenticToolSurface toolSurface,
    EnsureRepoSandboxToolFactory ensureRepoSandboxFactory,
    LoopLimitsConfig loopLimits,
    SubAgentBudget subAgentBudget,
    ISubAgentRunner subAgentRunner, SubAgentEvents subAgentEvents,
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
    // 2026-11-18-9c1cd: the surfaces are built in MasterToolComposition.Surfaces.cs, over these.
    private readonly AgenticToolSurface _surface = toolSurface;
    private readonly RenderReferenceToolFactory _references = renderReference;
    private readonly DesignReadToolHostFactory _design = designRead;
    private readonly EnsureRepoSandboxToolFactory _ensure = ensureRepoSandboxFactory;
    private readonly GateStageToolHost _gate = gateStage;
    private readonly TicketActToolsFactory _acts = ticketActs;
    private readonly DesignWorkToolGate _work = designWork;
    private readonly ILogger _logger = logger;
    // Children share the master's filesystem and base surface, never spawn_agents. A design turn fans out under its
    // own count and child ceiling and is kept off the escalation valve (a writable sandbox) and the ledger.
    // 2026-11-18-9c1cd: at most 20 tools per master, one per job, and the set fixed for the run or conversation — a
    // merged tool (MergedTools, MergedDialogTools) is offered on every turn and answers why when a part cannot act.
    public IList<AITool> Compose(
        bool isScanMaster, bool isSpecDialog, FilesystemToolHost fs, MasterRecordTools records, IToolHost human,
        WriteContextYamlToolHost writeContextYaml,
        WebToolHost? web, ProgressLedgerToolHost progress,
        WithdrawFiledTicketToolHost? withdraw, ReadTicketToolHost? readTicket,
        TicketWorkToolHost? ticketWork, TicketRunsToolHost? ticketRuns,
        AgenticMasterContext context, SubAgentTeam team)
    {
        Loop.Sanity.RefusedWriteLog.OpenFresh(context.Pipeline); // 2026-10-17-a196a: this step's refusals only
        // A design turn's ticket reads, for its children too (without the series, which only the master acts on).
        AIFunction? Ticket(bool withSeries, IReadOnlyList<AITool> acts) => readTicket is null ? null
            : MergedDialogTools.ReadTicket(
                MergedTools.Named(readTicket.GetTools(null, null), "read_ticket")!,
                ticketWork is null ? null : MergedTools.Named(ticketWork.GetTools(null, null), "ticket_work"),
                ticketRuns is null ? null : MergedTools.Named(ticketRuns.GetTools(null, null), "ticket_runs"),
                withSeries ? MergedTools.Named(acts, "read_series") : null,
                withSeries ? MergedTools.Named(acts, "verify_series") : null);
        // p0380: recall (read) + remember join EVERY master surface, including Review/scan; a design turn's recall is
        // offered whether or not its grounding found a store (2026-11-18-9c1cd), and says when there is none.
        IList<AITool> BaseSurface() => isSpecDialog
            ? [.. _surface.SpecDialog(fs, web, records.Recall), .. Ticket(false, []) is { } read ? [read] : Array.Empty<AITool>()]
            : isScanMaster
                ? _surface.Review(fs, records.Log, web, records.Recall, records.Remember)
                : _surface.ReadWriteWithHuman(
                    fs, records.Log, human, web: web, writeContextYaml: writeContextYaml,
                    recall: records.Recall, remember: records.Remember);

        var master = isSpecDialog ? DialogMaster(fs, records, web, withdraw, context, Ticket)
            : isScanMaster ? ScanMaster(BaseSurface(), records)
            : CodingMaster(BaseSurface(), fs, records, web, progress, context);
        // A design turn gates on ITS OWN count, so it fans out with the run-wide number at 0.
        var fanOut = isSpecDialog ? loopLimits.MaxSubAgentsPerDialogTurn : loopLimits.MaxSubAgentsPerRun;
        if (fanOut <= 0) return sanity.Wrap(master, context.Pipeline);

        // The injected budget's capacity is frozen from the run-wide number, so a turn that
        // only READ its own count would still be granted twenty. It builds the budget it gates
        // on and hands that one to the spawn host, which reserves from it.
        var budget = isSpecDialog ? new SubAgentBudget(fanOut, SubAgentBudget.DialogTurnKey, "design turn") : subAgentBudget;
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var rid) && rid is not null ? rid : "run";
        var sandboxes = context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var subCtx = new SubAgentContext(
            context.Pipeline, sandboxes, PipelineCostTracker.GetOrCreate(context.Pipeline), runId,
            ChildTools: sanity.Wrap(isSpecDialog ? reportingTools.Reporting(SpecDialogChildTools.Of(BaseSurface())) : BaseSurface(), context.Pipeline),
            Budget: budget, AgentConfig: context.AgentConfig,
            // No governor hooks on a child: how far it may go bounds the whole wave.
            ChildIterationCeiling: isSpecDialog ? context.AgentConfig.DialogSubAgentLoopIterations : null, // cd4cl
            Team: team); // 2026-11-18-c8e2a
        var spawn = new SpawnAgentToolHost(subAgentRunner, budget, subAgentNameValidator, decisionLogger, subCtx).GetTools(null, null);
        return sanity.Wrap([.. master, .. spawn, .. new SubAgentTeamToolHost(team, subAgentRunner, subCtx, decisionLogger, subAgentEvents).GetTools(null, null)], context.Pipeline);
    }
}
