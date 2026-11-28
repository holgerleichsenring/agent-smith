using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Renders a run's plan and result (RunResultFormatter) and stores them in the run artifact store, where the run page
/// reads them. RunId is generated once at pipeline start by ExecutePipelineUseCase and read from PipelineContext here.
/// <para>2026-11-28-db88b: the record lives with the run, in the database (the CLI's in-memory store), and nothing is
/// written under <c>.agentsmith/runs/</c> in a sandbox any more — one run-wide result with every repository's changes,
/// and on an init one result joining every repository's components. A plan the master recorded with record_plan is
/// never overwritten. Earlier runs' <c>runs/</c> directories stay in their repositories.</para>
/// </summary>
public sealed class WriteRunResultHandler(
    IDialogueTrail dialogueTrail,
    IRunArtifactStore artifactStore,
    IEventPublisher events,
    Memory.MemoryOutcomeRecorder outcomes, // 9324c: run-end memory standing
    RunResultChanges runResultChanges, // 2026-10-13-3890b: the branch is the record
    ILogger<WriteRunResultHandler> logger)
    : ICommandHandler<WriteRunResultContext>
{
    public async Task<CommandResult> ExecuteAsync(
        WriteRunResultContext context, CancellationToken cancellationToken)
    {
        var runId = context.Pipeline.Get<string>(ContextKeys.RunId);
        await PublishIgnoredInstructionsAsync(context, runId, cancellationToken);
        await PublishRunStoryAsync(context, runId, cancellationToken);
        await outcomes.RecordAsync(context.Pipeline, cancellationToken); // 2026-10-25-9324c: once, from final standing
        if (IsInitMode(context))
            return await WriteInitFanOutAsync(context, runId, cancellationToken);
        return await WriteSingleAsync(context, runId, cancellationToken);
    }

    // p0316: emit one TicketInstructionIgnored event per refused instruction (once per
    // run, before the per-repo record write) so the dashboard + audit trail see them.
    private async Task PublishIgnoredInstructionsAsync(
        WriteRunResultContext context, string runId, CancellationToken ct)
    {
        // 2026-11-22-96c8a: every spec's and pass's refusals, kept for the run.
        if (Specs.Reports.MasterReportFacts.Ignored(context.Pipeline) is not { Count: > 0 } ignored)
            return;
        foreach (var i in ignored)
        {
            try
            {
                await events.PublishAsync(
                    new TicketInstructionIgnoredEvent(runId, i.Quote, i.Reason, DateTimeOffset.UtcNow), ct);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to publish TicketInstructionIgnored event");
            }
        }
    }

    // 2026-11-17-713ea: an init is one whatever its door — a label-started init fetched its ticket
    // and still writes the init record. A ticketless run with no plan (a CLI scan) takes it too.
    private static bool IsInitMode(WriteRunResultContext context) =>
        InitRunSeed.IsInitRun(context.Pipeline) || (context.Ticket is null && context.Plan is null);

    // p0344b: snapshot the run story (p0341 progress ledger + p0340 acceptance
    // dispositions over the ratified criteria) onto the event stream so the
    // server-side applier persists both as JSON columns on the run row — the
    // stream is the only DB channel a spawned orchestrator has. Nothing to
    // record (no ledger AND no ratified contract) publishes nothing: the run
    // detail then serves honest nulls.
    private async Task PublishRunStoryAsync(
        WriteRunResultContext context, string runId, CancellationToken ct)
    {
        var ledgerJson = RunStorySnapshotBuilder.BuildLedgerJson(
            TryGet<ProgressLedger>(context.Pipeline, ContextKeys.ProgressLedger));
        // 2026-08-25-7f5a: the account the gate decided on, where the run took one. A run
        // that failed at Verify still carries it — the accounts are recorded before the
        // failing verdict and the finalizer tail runs after a failed step.
        var acceptanceJson = RunStorySnapshotBuilder.BuildAcceptanceJson(Specs.RunAccountLedger.Current(context.Pipeline));
        if (ledgerJson is null && acceptanceJson is null) return;
        try
        {
            await events.PublishAsync(
                new RunStoryRecordedEvent(runId, ledgerJson, acceptanceJson, DateTimeOffset.UtcNow), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish RunStoryRecorded event for {RunId}", runId);
        }
    }

    private async Task<CommandResult> WriteSingleAsync(
        WriteRunResultContext context, string runId, CancellationToken cancellationToken)
    {
        var (cost, duration) = ResolveCostAndDuration(context.Pipeline);
        var trail = TryGet<List<ExecutionTrailEntry>>(context.Pipeline, ContextKeys.ExecutionTrail);
        var repos = context.Pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is { Count: > 0 } ? r : null;
        var changeSet = await runResultChanges.ResolveAsync(context.Pipeline, repos, context.Changes, cancellationToken);
        var decisions = WithDiagnostics(
            TryGet<List<PlanDecision>>(context.Pipeline, ContextKeys.Decisions), context.Pipeline, changeSet);
        var trend = TryGet<SecurityTrend>(context.Pipeline, ContextKeys.SecurityTrend);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(context.Pipeline);
        var topology = ResolveTopology(context.Pipeline, runId);

        // p0380: the early verdict is computed ONCE here (it is pipeline-scoped) and the result renders it.
        var failureReason = ResolveFailureReason(context);

        // 2026-11-28-db88b: one run-wide record — every repository's changes, the run's whole cost.
        await StoreRunRecordAsync(context, runId, changeSet.All, cost, duration, trail, decisions, trend,
            dialogueEntries, perSkillBreakdown, topology, failureReason, cancellationToken);

        // 2026-11-28-db88c: no per-ticket memory file — the run's narrative is this record; memory/ holds what
        // a master keeps with remember.
        logger.LogInformation("Recorded run result {RunId}", RunIdGenerator.FormatForDisplay(runId));
        return CommandResult.Ok(repos is null
            ? $"Run {RunIdGenerator.FormatForDisplay(runId)} recorded"
            : $"Run {RunIdGenerator.FormatForDisplay(runId)} recorded for {repos.Count} repo(s)");
    }

    // p0341: done-status honesty DIAGNOSTIC — a done ledger step whose EXPLICIT target is
    // absent from the run's files is a decision line, never a gate. 2026-10-13-3890b: read
    // against every repository's files, and beside a note per sandbox whose branch was unreadable.
    private static List<PlanDecision>? WithDiagnostics(
        List<PlanDecision>? decisions, PipelineContext pipeline, RunResultChangeSet changes)
    {
        var ledger = TryGet<ProgressLedger>(pipeline, ContextKeys.ProgressLedger);
        var warnings = ledger is { IsEmpty: false }
            ? Tools.ProgressLedgerCoverage.UnbackedDoneSteps(ledger, changes.Coverage)
                .Select(w => $"[progress-ledger] {w} (diagnostic, not a gate)")
            : [];
        var lines = changes.Notes.Concat(warnings).ToList();
        if (lines.Count == 0) return decisions;
        decisions ??= [];
        decisions.AddRange(lines.Select(line => new PlanDecision("Implementation", line)));
        return decisions;
    }

    // p0380: explicit failure set by a prior step wins; otherwise the p0253
    // early keystone verdict.
    private static string? ResolveFailureReason(WriteRunResultContext context) =>
        context.Pipeline.TryGet<string>(ContextKeys.FailureReason, out var fr)
        && !string.IsNullOrWhiteSpace(fr)
            ? fr
            : EarlyKeystoneFailure(context);

    // The run's plan and result, stored for the run page. A structured preset's plan is rendered; a coding master's
    // is what it recorded with record_plan, which this never overwrites.
    private async Task StoreRunRecordAsync(
        WriteRunResultContext context, string runId, IReadOnlyList<CodeChange> changes,
        RunCostSummary? cost, int duration, List<ExecutionTrailEntry>? trail,
        List<PlanDecision>? decisions, SecurityTrend? trend,
        IReadOnlyList<DialogTrailEntry> dialogueEntries, IReadOnlyList<CallCostRecord>? perSkillBreakdown,
        RunMetaTopology topology, string? failureReason, CancellationToken ct)
    {
        if (context.Plan is not null)
            await TryStorePlanAsync(runId, RunResultFormatter.FormatPlan(context.Ticket!, context.Plan), ct);

        // p0253: the result is rendered BEFORE CommitAndPR runs the git-authoritative keystone, so the verdict
        // (failureReason) is computed once in WriteSingleAsync and passed in — the result never claims success for
        // a run that changed no real source or failed verification.
        var ignoredInstructions = Specs.Reports.MasterReportFacts.Ignored(context.Pipeline) is { Count: > 0 } ii ? ii : null;
        var resultMd = RunResultFormatter.FormatResult(
            context.Ticket!, context.Plan, changes, runId, duration, cost, trail, decisions, trend,
            dialogueEntries.Count > 0 ? dialogueEntries : null, perSkillBreakdown, topology, repoName: null, failureReason,
            ignoredInstructions,
            // p0429a: what the run — or the scan — accounted for, itemised beside the
            // findings instead of only inside the gate that read it.
            RunAccountSection.BuildForResult(context.Pipeline, failureReason), // 3890b: once
            VisualComparisonSection.Build(context.Pipeline)); // + 283di
        await TryStoreResultAsync(runId, resultMd, ct);
    }

    // p0161d: an init discovers per repository; 2026-11-28-db88b: its record is ONE plan and ONE result that join
    // every repository's components, not one per repository where the last overwrote the rest.
    private async Task<CommandResult> WriteInitFanOutAsync(
        WriteRunResultContext context, string runId, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is { Count: > 0 } ? r : null;
        var (cost, duration) = ResolveCostAndDuration(pipeline);
        var trail = TryGet<List<ExecutionTrailEntry>>(pipeline, ContextKeys.ExecutionTrail);
        var decisions = TryGet<List<PlanDecision>>(pipeline, ContextKeys.Decisions);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(pipeline);
        if (repos is null)
        {
            await TryStoreResultAsync(runId, RunResultFormatter.FormatInitResult(
                runId, duration, cost, trail, decisions,
                dialogueEntries.Count > 0 ? dialogueEntries : null, perSkillBreakdown), cancellationToken);
            return CommandResult.Ok($"Run {RunIdGenerator.FormatForDisplay(runId)} recorded");
        }

        var components = TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            pipeline, ContextKeys.DiscoveredComponents);
        var outputs = TryGet<Dictionary<string, Dictionary<string, string>>>(
            pipeline, ContextKeys.BootstrapOutputs);
        var retired = TryGet<IReadOnlyDictionary<string, IReadOnlyList<string>>>(pipeline, ContextKeys.RetiredContexts);
        var records = repos.Select(repo => new InitRepoRecord(
            repo.Name,
            components is not null && components.TryGetValue(repo.Name, out var rc) ? rc : null,
            outputs is not null && outputs.TryGetValue(repo.Name, out var ro) ? ro : null,
            retired?.GetValueOrDefault(repo.Name))).ToList(); // 2026-09-23-4711
        var sharedNote = repos.Count > 1
            ? $"Total run cost shared across {repos.Count} repos — Discover allocated to each repo equally; BootstrapRound calls already tagged per repo."
            : null;

        await TryStorePlanAsync(runId, string.Join("\n", records.Select(
            record => RunResultFormatter.FormatInitPlan(runId, record.RepoName, record.Components))), cancellationToken);
        await TryStoreResultAsync(runId, RunResultFormatter.FormatInitResult(
            runId, duration, cost, trail, decisions,
            dialogueEntries.Count > 0 ? dialogueEntries : null, perSkillBreakdown, records, sharedNote), cancellationToken);
        logger.LogInformation("Recorded init run result {RunId} ({Repos} repo(s))",
            RunIdGenerator.FormatForDisplay(runId), repos.Count);
        return CommandResult.Ok(
            $"Run {RunIdGenerator.FormatForDisplay(runId)} recorded for {repos.Count} repo(s)");
    }

    private async Task TryStoreResultAsync(string runId, string resultMd, CancellationToken ct)
    {
        try
        {
            await artifactStore.WriteResultMarkdownAsync(runId, resultMd, ct);
        }
        catch (Exception ex)
        {
            // 2026-11-28-db88b: the store holds the run's only copy of its result.
            logger.LogError(ex, "Failed to store the result of {RunId} in the artifact store", runId);
        }
    }

    private async Task TryStorePlanAsync(string runId, string planMd, CancellationToken ct)
    {
        try
        {
            // 2026-11-28-db88b: a plan the master recorded with record_plan is the run's plan.
            if (!string.IsNullOrWhiteSpace(await artifactStore.ReadPlanMarkdownAsync(runId, ct))) return;
            await artifactStore.WritePlanMarkdownAsync(runId, planMd, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to store the plan of {RunId} in the artifact store", runId);
        }
    }

    private static (RunCostSummary? Cost, int DurationSeconds) ResolveCostAndDuration(PipelineContext pipeline)
    {
        var cost = pipeline.TryGet<RunCostSummary>(ContextKeys.RunCostSummary, out var explicitCost)
            && explicitCost is not null
            ? explicitCost
            : RecordedCallSummary.For(pipeline) ?? PipelineCostTracker.GetOrCreate(pipeline).BuildSummary(); // 7c9bd
        // 2026-10-13-3890b: wall clock — a parked-then-resumed run counts its parked time.
        var duration = pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var startedAt)
            ? (int)Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalSeconds)
            : 0;
        return (cost, duration);
    }

    private static T? TryGet<T>(PipelineContext pipeline, string key) where T : class
        => pipeline.TryGet<T>(key, out var value) ? value : null;

    /// <summary>
    /// p0169a: harvest repos / repo_mode / sandbox_count / pipeline_name /
    /// status / started_at from <see cref="PipelineContext"/> so the dashboard
    /// can render topology badges without re-deriving from execution-trail.
    /// </summary>
    private static RunMetaTopology ResolveTopology(PipelineContext pipeline, string runId)
    {
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var rs) && rs is not null
            ? rs.Select(r => r.Name).ToList()
            : null;
        var repoMode = repos is { Count: > 1 } ? "multi" : "mono";

        int sandboxCount = 0;
        if (pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes) && sandboxes is not null)
            sandboxCount = sandboxes.Count;
        else if (pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out _))
            sandboxCount = 1;

        var pipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null;
        var startedAt = pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var st)
            ? (DateTimeOffset?)st : null;

        return new RunMetaTopology(
            RunId: runId,
            PipelineName: pipelineName,
            Status: "done",
            StartedAt: startedAt,
            RepoMode: repoMode,
            SandboxCount: sandboxCount,
            Repos: repos);
    }

    // p0253: the run verdict for result.md, computed BEFORE CommitAndPR's
    // git-authoritative keystone. gitCommittedChange is proxied by recordedChange
    // (real, non-record changes) — the git-truth keystone in CommitAndPR stays the
    // hard gate; this only keeps result.md consistent with it for the common cases
    // (no real changes / failed verification). Returns the failure reason, or null.
    private static string? EarlyKeystoneFailure(WriteRunResultContext context)
    {
        var pipelineName = context.Pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) && pn is not null
            ? pn : string.Empty;
        // p0421: the same ONE gate CommitAndPR applies, reading the same accounts —
        // result.md and the pull request cannot disagree about whether a run delivered
        // because they no longer answer the question separately.
        var verdict = RunDeliveryGate.Mechanical( // c8e2c
            Specs.RunAccountLedger.Current(context.Pipeline), Specs.AcceptanceCriteria.For(context.Pipeline).Count);
        return verdict.Satisfied ? null : verdict.FailureReason;
    }

    private static IReadOnlyList<CallCostRecord>? ResolvePerSkillBreakdown(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<PipelineCostTracker>("PipelineCostTracker", out var tracker)
            || tracker is null)
            return null;
        var breakdown = tracker.PerSkillBreakdown;
        return breakdown.Count == 0 ? null : breakdown;
    }

}
