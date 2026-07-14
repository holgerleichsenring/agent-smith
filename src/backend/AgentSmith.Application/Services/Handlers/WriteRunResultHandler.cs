using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Writes run artifacts (plan.md + result.md) to
/// <c>.agentsmith/runs/{RunId}-{slug}/</c>. Formatting is delegated to
/// RunResultFormatter. RunId is generated once at pipeline start by
/// ExecutePipelineUseCase and read from PipelineContext here.
/// <para>p0287: the per-run <c>runs:</c> ledger that used to be appended to
/// <c>.agentsmith/context.yaml</c> was dropped — the run record is the
/// <c>runs/{runId}/</c> directory (plus the RunEvent/RunStep DB tables), and a
/// duplicate ledger that shared the project-context filename added no value and
/// conflated bookkeeping with the project context.</para>
///
/// p0161d: init-project runs fan out per <see cref="RepoConnection"/>: each
/// repo's sandbox gets its own <c>runs/{runId}-init/{plan.md,result.md}</c>
/// with that repo's slice of <see cref="ContextKeys.DiscoveredComponents"/>
/// and <see cref="ContextKeys.BootstrapOutputs"/>. Same RunId across all
/// repos. Ticket-driven runs keep the single-sandbox path.
/// </summary>
public sealed class WriteRunResultHandler(
    ISandboxFileReaderFactory readerFactory,
    IDialogueTrail dialogueTrail,
    IRunArtifactStore artifactStore,
    IEventPublisher events,
    ILogger<WriteRunResultHandler> logger)
    : ICommandHandler<WriteRunResultContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string RunsDir = "runs";
    // p0237: breadcrumb left at the loose <repo>/.agentsmith/plan.md after the
    // plan is relocated into the run dir; also the guard that stops a later run
    // from treating the breadcrumb as a real plan to relocate again.
    private const string PlanPointerPrefix = "Plan moved to";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<CommandResult> ExecuteAsync(
        WriteRunResultContext context, CancellationToken cancellationToken)
    {
        var runId = context.Pipeline.Get<string>(ContextKeys.RunId);
        await PublishIgnoredInstructionsAsync(context, runId, cancellationToken);
        if (IsInitMode(context))
            return await WriteInitFanOutAsync(context, runId, cancellationToken);
        return await WriteSingleAsync(context, runId, cancellationToken);
    }

    // p0316: emit one TicketInstructionIgnored event per refused instruction (once per
    // run, before the per-repo record write) so the dashboard + audit trail see them.
    private async Task PublishIgnoredInstructionsAsync(
        WriteRunResultContext context, string runId, CancellationToken ct)
    {
        if (!context.Pipeline.TryGet<MasterVerification>(ContextKeys.MasterVerification, out var mv)
            || mv?.IgnoredInstructions is not { Count: > 0 } ignored)
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

    private static bool IsInitMode(WriteRunResultContext context) =>
        context.Ticket is null && context.Plan is null;

    private async Task<CommandResult> WriteSingleAsync(
        WriteRunResultContext context, string runId, CancellationToken cancellationToken)
    {
        var (cost, duration) = ResolveCostAndDuration(context.Pipeline);
        var trail = TryGet<List<ExecutionTrailEntry>>(context.Pipeline, ContextKeys.ExecutionTrail);
        var decisions = TryGet<List<PlanDecision>>(context.Pipeline, ContextKeys.Decisions);
        var trend = TryGet<SecurityTrend>(context.Pipeline, ContextKeys.SecurityTrend);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(context.Pipeline);
        var topology = ResolveTopology(context.Pipeline, runId);

        // p0234: write a run-record PER REPO. There is no "primary" repo — each
        // repo gets its own .agentsmith/runs/{runId}/{plan,result}.md, scoped to
        // ITSELF: cost via RunCostSummary.PerRepo[repo] (FormatResult's repoName
        // arg), changes filtered by the repo prefix. So every repo carries a
        // committable run-record and gets its own PR. Falls back to the legacy
        // single-sandbox write when the run has no Repos list (CLI/one-off).
        var repos = context.Pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is { Count: > 0 } ? r : null;

        if (repos is null)
        {
            var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
            await WriteRepoRecordAsync(
                readerFactory.Create(sandbox), context, runId, repoName: null, context.Changes,
                cost, duration, trail, decisions, trend, dialogueEntries, perSkillBreakdown, topology,
                cacheResult: true, tryCachePlan: true, cancellationToken);
            logger.LogInformation("Written run result {RunId} (single sandbox)", RunIdGenerator.FormatForDisplay(runId));
            return CommandResult.Ok($"Run {RunIdGenerator.FormatForDisplay(runId)} recorded");
        }

        var written = 0;
        var planCached = false;
        foreach (var repo in repos)
        {
            var sandbox = ResolvePerRepoSandbox(context.Pipeline, repo);
            if (sandbox is null)
            {
                logger.LogWarning("WriteRunResult: no sandbox for repo '{Repo}' — skipping run-doc", repo.Name);
                continue;
            }
            var repoChanges = repos.Count == 1
                ? context.Changes
                : context.Changes
                    .Where(c => c.Path.ToString().StartsWith(repo.Name + "/", StringComparison.Ordinal))
                    .ToList();
            // cacheResult on the first repo; cache the first plan.md we find (the
            // agent may write its plan.md only in the repo it edited, p0235).
            planCached |= await WriteRepoRecordAsync(
                readerFactory.Create(sandbox), context, runId, repo.Name, repoChanges,
                cost, duration, trail, decisions, trend, dialogueEntries, perSkillBreakdown, topology,
                cacheResult: written == 0, tryCachePlan: !planCached, cancellationToken);
            written++;
            logger.LogInformation(
                "WriteRunResult: repo {Repo} run-doc written ({RunId})", repo.Name, RunIdGenerator.FormatForDisplay(runId));
        }
        return CommandResult.Ok(
            $"Run {RunIdGenerator.FormatForDisplay(runId)} recorded in {written} repo(s)");
    }

    // p0234: write one repo's run-record (plan.md + result.md + context.yaml
    // entry) scoped to that repo. Shared by the per-repo fan-out and the legacy
    // single-sandbox fallback. Returns true when it cached a plan.md for the
    // dashboard (p0235), so the per-repo loop caches the first one it finds.
    private async Task<bool> WriteRepoRecordAsync(
        ISandboxFileReader reader, WriteRunResultContext context, string runId,
        string? repoName, IReadOnlyList<CodeChange> repoChanges,
        RunCostSummary? cost, int duration, List<ExecutionTrailEntry>? trail,
        List<PlanDecision>? decisions, SecurityTrend? trend,
        IReadOnlyList<DialogTrailEntry> dialogueEntries, IReadOnlyList<CallCostRecord>? perSkillBreakdown,
        RunMetaTopology topology, bool cacheResult, bool tryCachePlan, CancellationToken ct)
    {
        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var runDir = Path.Combine(agentDir, RunsDir, RunRecordPaths.DirName(runId));

        // p0196: coding presets retire GeneratePlan (Plan null) → no rendered
        // plan.md. p0235/p0237: in that case the plan is the agent's own
        // <repo>/.agentsmith/plan.md (coding-agent-master writes it in Phase 1,
        // before the run dir exists). RELOCATE it into the per-run record so it
        // is preserved per run — the loose path is overwritten every run — and
        // leave a one-line pointer breadcrumb there.
        string? planMd = null;
        if (context.Plan is not null)
        {
            planMd = RunResultFormatter.FormatPlan(context.Ticket!, context.Plan);
            await reader.WriteAsync(Path.Combine(runDir, "plan.md"), planMd, ct);
        }
        else
        {
            // p0244: coding-agent-master now writes its plan.md DIRECTLY into the
            // per-run dir (it gets {RunRecordDir} in its prompt) — read it there.
            // Fall back to the legacy loose <repo>/.agentsmith/plan.md + relocate
            // for skills that predate the run-dir instruction.
            var runDirPlan = Path.Combine(runDir, "plan.md");
            planMd = await reader.TryReadAsync(runDirPlan, ct);
            if (string.IsNullOrWhiteSpace(planMd))
            {
                var loosePlanPath = Path.Combine(agentDir, "plan.md");
                var loosePlan = await reader.TryReadAsync(loosePlanPath, ct);
                if (!string.IsNullOrWhiteSpace(loosePlan)
                    && !loosePlan.StartsWith(PlanPointerPrefix, StringComparison.Ordinal))
                {
                    planMd = loosePlan;
                    await reader.WriteAsync(runDirPlan, planMd, ct);
                    await reader.WriteAsync(loosePlanPath,
                        $"{PlanPointerPrefix} runs/{runId}/plan.md (per-run record).\n", ct);
                }
            }
        }
        await WriteOptionalArtifactsAsync(reader, runDir, context.Pipeline, ct);

        // p0253: result.md is rendered BEFORE CommitAndPR runs the git-authoritative
        // keystone, so a failing run used to render result:success. Use an explicit
        // failure if a prior step already set one; otherwise compute the verdict now
        // (see EarlyKeystoneFailure) so result.md never claims success for a run that
        // changed no real source / failed verification.
        var failureReason = context.Pipeline.TryGet<string>(ContextKeys.FailureReason, out var fr)
            && !string.IsNullOrWhiteSpace(fr)
            ? fr
            : EarlyKeystoneFailure(context);
        var ignoredInstructions = context.Pipeline.TryGet<MasterVerification>(
            ContextKeys.MasterVerification, out var mv) && mv?.IgnoredInstructions is { Count: > 0 } ii
            ? ii : null;
        // p0328: the ratified expectation renders on the run record as the
        // acceptance-contract checklist (unratified stamp for headless runs).
        var expectation = context.Pipeline.TryGet<Contracts.Expectations.RatifiedExpectation>(
            ContextKeys.RunExpectation, out var exp) ? exp : null;
        var resultMd = RunResultFormatter.FormatResult(
            context.Ticket!, context.Plan, repoChanges, runId, duration, cost, trail, decisions, trend,
            dialogueEntries.Count > 0 ? dialogueEntries : null, perSkillBreakdown, topology, repoName, failureReason,
            ignoredInstructions, expectation);
        await reader.WriteAsync(Path.Combine(runDir, "result.md"), resultMd, ct);
        if (cacheResult) await TryStoreResultAsync(runId, resultMd, ct);

        var planCached = false;
        if (tryCachePlan && !string.IsNullOrWhiteSpace(planMd))
        {
            await TryStorePlanAsync(runId, planMd, ct);
            planCached = true;
        }
        return planCached;
    }

    private async Task<CommandResult> WriteInitFanOutAsync(
        WriteRunResultContext context, string runId, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is { Count: > 0 } ? r : null;
        if (repos is null)
            return await WriteInitSingleSandboxAsync(context, runId, cancellationToken);

        var components = TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            pipeline, ContextKeys.DiscoveredComponents);
        var outputs = TryGet<Dictionary<string, Dictionary<string, string>>>(
            pipeline, ContextKeys.BootstrapOutputs);
        var (cost, duration) = ResolveCostAndDuration(pipeline);
        var trail = TryGet<List<ExecutionTrailEntry>>(pipeline, ContextKeys.ExecutionTrail);
        var decisions = TryGet<List<PlanDecision>>(pipeline, ContextKeys.Decisions);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(pipeline);
        var sharedNote = repos.Count > 1
            ? $"Total run cost shared across {repos.Count} repos — Discover allocated to each repo equally; BootstrapRound calls already tagged per repo."
            : null;

        var written = 0;
        foreach (var repo in repos)
        {
            var sandbox = ResolvePerRepoSandbox(pipeline, repo);
            if (sandbox is null)
            {
                logger.LogWarning("WriteRunResult: no sandbox for repo '{Repo}' — skipping init run-doc", repo.Name);
                continue;
            }
            var reader = readerFactory.Create(sandbox);
            var repoComponents = components is not null && components.TryGetValue(repo.Name, out var rc) ? rc : null;
            var repoOutputs = outputs is not null && outputs.TryGetValue(repo.Name, out var ro) ? ro : null;
            var runDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir, RunsDir, $"{runId}-init");

            var planMd = RunResultFormatter.FormatInitPlan(runId, repo.Name, repoComponents);
            await reader.WriteAsync(Path.Combine(runDir, "plan.md"), planMd, cancellationToken);
            if (written == 0) await TryStorePlanAsync(runId, planMd, cancellationToken);
            await WriteOptionalArtifactsAsync(reader, runDir, pipeline, cancellationToken);
            var resultMd = RunResultFormatter.FormatInitResult(
                runId, duration, cost, trail, decisions,
                dialogueEntries.Count > 0 ? dialogueEntries : null,
                perSkillBreakdown,
                repoName: repo.Name, components: repoComponents,
                bootstrapOutputsByContext: repoOutputs,
                sharedCostNote: sharedNote);
            await reader.WriteAsync(Path.Combine(runDir, "result.md"), resultMd, cancellationToken);
            await TryStoreResultAsync(runId, resultMd, cancellationToken);
            written++;
            logger.LogInformation(
                "WriteRunResult: repo {Repo} init run-doc written to {Dir}", repo.Name, Path.GetFileName(runDir));
        }

        return CommandResult.Ok(
            $"Run {RunIdGenerator.FormatForDisplay(runId)} recorded in {written} repo(s)");
    }

    private async Task<CommandResult> WriteInitSingleSandboxAsync(
        WriteRunResultContext context, string runId, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var runDir = Path.Combine(agentDir, RunsDir, $"{runId}-init");
        await WriteOptionalArtifactsAsync(reader, runDir, context.Pipeline, cancellationToken);
        var (cost, duration) = ResolveCostAndDuration(context.Pipeline);
        var trail = TryGet<List<ExecutionTrailEntry>>(context.Pipeline, ContextKeys.ExecutionTrail);
        var decisions = TryGet<List<PlanDecision>>(context.Pipeline, ContextKeys.Decisions);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(context.Pipeline);
        var resultMd = RunResultFormatter.FormatInitResult(
            runId, duration, cost, trail, decisions,
            dialogueEntries.Count > 0 ? dialogueEntries : null, perSkillBreakdown);
        await reader.WriteAsync(Path.Combine(runDir, "result.md"), resultMd, cancellationToken);
        await TryStoreResultAsync(runId, resultMd, cancellationToken);
        return CommandResult.Ok($"Run {RunIdGenerator.FormatForDisplay(runId)} recorded in {Path.GetFileName(runDir)}");
    }

    private async Task TryStoreResultAsync(string runId, string resultMd, CancellationToken ct)
    {
        try
        {
            await artifactStore.WriteResultMarkdownAsync(runId, resultMd, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to cache result.md for {RunId} in artifact store — disk write + PR remain authoritative",
                runId);
        }
    }

    private async Task TryStorePlanAsync(string runId, string planMd, CancellationToken ct)
    {
        try
        {
            await artifactStore.WritePlanMarkdownAsync(runId, planMd, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to cache plan.md for {RunId} in artifact store — disk write + PR remain authoritative",
                runId);
        }
    }

    private static ISandbox? ResolvePerRepoSandbox(PipelineContext pipeline, RepoConnection repo)
    {
        var matches = SandboxTargets.SandboxesForRepo(pipeline, repo);
        if (matches.Count > 0) return matches[0].Value;
        return pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var legacy) ? legacy : null;
    }

    private static (RunCostSummary? Cost, int DurationSeconds) ResolveCostAndDuration(PipelineContext pipeline)
    {
        var cost = pipeline.TryGet<RunCostSummary>(ContextKeys.RunCostSummary, out var explicitCost)
            && explicitCost is not null
            ? explicitCost
            : PipelineCostTracker.GetOrCreate(pipeline).BuildSummary();
        var duration = pipeline.TryGet<int>(ContextKeys.RunDurationSeconds, out var explicitSeconds)
            && explicitSeconds > 0
            ? explicitSeconds
            : pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var startedAt)
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

    /// <summary>
    /// p0128a: persists the structured plan/diff/bootstrap artifacts alongside the
    /// existing markdown when present. Source-of-truth for replay scenarios.
    /// </summary>
    private static async Task WriteOptionalArtifactsAsync(
        ISandboxFileReader reader, string runDir, PipelineContext pipeline, CancellationToken ct)
    {
        if (pipeline.TryGet<string>(ContextKeys.PlanJson, out var planJson) && !string.IsNullOrEmpty(planJson))
            await reader.WriteAsync(Path.Combine(runDir, "plan.json"), PrettyPrint(planJson), ct);

        if (pipeline.TryGet<string>(ContextKeys.DiffJson, out var diffJson) && !string.IsNullOrEmpty(diffJson))
            await reader.WriteAsync(Path.Combine(runDir, "diff.json"), PrettyPrint(diffJson), ct);

        if (pipeline.TryGet<string>(ContextKeys.BootstrapMarkdown, out var bootstrapMd)
            && !string.IsNullOrEmpty(bootstrapMd))
            await reader.WriteAsync(Path.Combine(runDir, "bootstrap.md"), bootstrapMd, ct);
    }

    private static string PrettyPrint(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
        }
        catch (JsonException)
        {
            return raw;
        }
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
        var verification = context.Pipeline.TryGet<MasterVerification>(ContextKeys.MasterVerification, out var mv)
            ? mv : null;
        var realChanges = context.Changes.Count(c => !RunRecordPaths.IsRunRecordPath(c.Path.ToString()));
        var verdict = RunOutcomeKeystone.Evaluate(
            PipelinePresets.ExpectsCodeChanges(pipelineName),
            PipelinePresets.ExpectsGreenTests(pipelineName),
            gitCommittedChange: realChanges > 0,
            recordedChange: realChanges > 0,
            verification,
            RatifiedCriteria(context.Pipeline));
        return verdict.Satisfied ? null : verdict.FailureReason;
    }

    // p0340: the ratified acceptance contract's Expected assertions — the criteria
    // the keystone gates on. Empty when the run negotiated no expectation (many
    // fix-bug runs, non-contract presets), which the keystone treats as "fall back
    // to the change+green gate".
    private static IReadOnlyList<string> RatifiedCriteria(PipelineContext pipeline) =>
        pipeline.TryGet<RatifiedExpectation>(ContextKeys.RunExpectation, out var exp) && exp is not null
            ? exp.Draft.Expected
            : Array.Empty<string>();

    private static IReadOnlyList<CallCostRecord>? ResolvePerSkillBreakdown(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<PipelineCostTracker>("PipelineCostTracker", out var tracker)
            || tracker is null)
            return null;
        var breakdown = tracker.PerSkillBreakdown;
        return breakdown.Count == 0 ? null : breakdown;
    }

}
