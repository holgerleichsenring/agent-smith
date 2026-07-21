using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0179b: runs a master skill body (resolved from IPromptCatalog by name
/// — typically "coding-agent-master") in one agentic loop. The master
/// decides plan + execute + verify internally; no choreography handlers
/// are involved. Coding pipelines (fix-bug, add-feature, fix-no-test)
/// dispatch this handler instead of the
/// Triage→GeneratePlan→…→AgenticExecute chain.
/// </summary>
public sealed class AgenticMasterHandler(
    IAgenticLoopRunner loopRunner,
    IPromptCatalog prompts,
    IDecisionLogger decisionLogger,
    AgentSmithConfig config,
    IContextYamlSerializer contextYamlSerializer,
    IMasterOutputSchemaResolver schemaResolver,
    IScanMasterPromptFactory scanPromptFactory,
    ISpecDialogPromptFactory specDialogPromptFactory,
    IPhaseExecutionPromptFactory phasePromptFactory,
    IOutcomeProposalResolver outcomeResolver,
    ISubAgentRunner subAgentRunner,
    SubAgentBudget subAgentBudget,
    SubAgentNameValidator subAgentNameValidator,
    IChildAnswerStore childAnswerStore,
    LoopLimitsConfig loopLimits,
    ITicketDocumentMaterializer documentMaterializer,
    EnsureRepoSandboxToolFactory ensureRepoSandboxFactory, // p0331
    WebToolHost webToolHost,
    IEventPublisher eventPublisher, // p0356: mid-run ledger flushes
    IPriorRunLedgerReader priorRunLedgerReader, // p0356: same-ticket resume seed
    ISandboxToolchainProbe toolchainProbe, // p0356: probed capability line
    RunWorkCheckpointer checkpointer, // p0360: mid-run work durability
    IDialogueTransport? dialogueTransport,
    ILogger<AgenticMasterHandler> logger)
    : ICommandHandler<AgenticMasterContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AgenticMasterContext context, CancellationToken cancellationToken)
    {
        var sandboxes = context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var defaultKey = sandboxes.Keys.First();
        // p0250: the authoritative sandbox-key → repo-name map (coordinator-published
        // since p0249). The master addresses repos by NAME and the tool host aliases
        // each name to its sandbox via this map, so the agent's write lands in the
        // SAME sandbox CommitAndPR commits from. Distinct repo names are what the
        // prompt lists (not the composite `<repo>-<langSlug>` toolchain keys).
        var keyToRepo = context.Pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.SandboxRepos, out var kr) && kr is not null ? kr : null;
        IReadOnlyList<string> addressNames = keyToRepo is not null
            ? keyToRepo.Values.Distinct(StringComparer.Ordinal).ToList()
            : sandboxes.Keys.ToList();

        var ticket = context.Pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) && t is not null
            ? t
            : null;
        var pipelineName = context.Pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null;
        // p0315b: the spec-dialog conversation branch. Keyed on the PIPELINE (the
        // dialog IS the pipeline's identity) rather than the master's output_schema:
        // an output_schema value outside the loader's closed {observation, plan, diff,
        // bootstrap, discovery} set would fail catalog validation on every already-
        // deployed server, so the skill deliberately declares none.
        var isSpecDialog = string.Equals(
            pipelineName, PipelinePresets.SpecDialogName, StringComparison.OrdinalIgnoreCase);
        // p0315d: the phase-execution branch, keyed on the pipeline name like
        // spec-dialog — same master (coding-agent-master), phase-specific user
        // prompt + a ticket-parking ask_human instead of the live transport.
        var isPhaseExecution = string.Equals(
            pipelineName, PipelinePresets.PhaseExecutionName, StringComparison.OrdinalIgnoreCase);
        // p0244: give the master the per-run record dir so it writes plan.md /
        // decisions.md DIRECTLY into .agentsmith/runs/{runId}/ (the same dir the
        // framework writes result.md to + reads the plan back from), instead of a
        // loose .agentsmith/plan.md that gets overwritten every run.
        var runRecordDir = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var rid)
            && !string.IsNullOrEmpty(rid)
            ? RunRecordPaths.RelativeDir(rid!)
            : RunRecordPaths.AgentSmithDir;

        // p0276: the operator-approved plan (GeneratePlan, before Approval) is
        // rendered into the master body so it EXECUTES that plan rather than
        // re-planning from scratch. Empty when no plan was generated (other presets).
        var plan = context.Pipeline.TryGet<Domain.Entities.Plan>(ContextKeys.Plan, out var pl) ? pl : null;
        // p0278: a scan/review master (output_schema == observation) gets the scanner
        // findings + spec inline and a READ-ONLY surface. Keyed on the master's
        // declared schema, NOT pipeline name; computed HERE (p0356) because the
        // ledger seed + toolchain probe below are coding-master-only concerns.
        var isScanMaster = string.Equals(
            schemaResolver.Resolve(context.MasterSkillName), "observation", StringComparison.OrdinalIgnoreCase);

        // p0341: seed the durable progress ledger 1:1 from the ratified plan (stable
        // framework ids + per-step target) so the master opens on the checklist. Also
        // published to PipelineContext (source of truth) for the re-drive nudges + the
        // done-status diagnostic. No plan (fix-bug self-planning) => empty seed.
        // p0356: a plan-less coding run of a TICKET seen before resumes on the latest
        // prior run's persisted ledger (mid-run flushes make it durable) — gated in
        // PriorRunLedgerSeeder on progressed-past-bootstrap + the age cap.
        var seedEntries = ProgressLedgerSeeder.Seed(plan);
        if (seedEntries.Count == 0 && !isScanMaster && !isSpecDialog && ticket is not null)
            seedEntries = await SeedFromPriorRunAsync(ticket, cancellationToken);
        // p0356: every accepted update_progress replace flushes the ledger onto the
        // event stream — resume-after-reap needs the ledger DURABLE mid-run, not
        // only at WriteRunResult. The flush is AWAITED by the tool call so it never
        // outlives the handler. Run-record-less contexts (no run id) skip it.
        var flusher = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var flushRunId)
            && !string.IsNullOrEmpty(flushRunId)
            ? new ProgressLedgerFlusher(eventPublisher, flushRunId!, logger)
            : null;
        // p0360: every accepted replace ALSO checkpoints the work itself — commit +
        // push of each dirty repo sandbox to the run branch (throttled, secret-
        // scanned). The ledger flip is the natural work-unit boundary, and p0359's
        // staleness reminder keeps that boundary firing; together a dying run
        // leaves both its checklist AND its edits behind. Coding masters only —
        // scan/spec-dialog surfaces have no update_progress tool.
        var checkpointInterval = context.AgentConfig.CheckpointPushMinIntervalSeconds;
        Func<Contracts.Progress.ProgressLedger, Task>? onReplaced =
            flusher is null && checkpointInterval <= 0
                ? null
                : async ledger =>
                {
                    if (flusher is not null) await flusher.FlushAsync(ledger);
                    await checkpointer.CheckpointAsync(
                        context.Pipeline, checkpointInterval, cancellationToken);
                };
        var progress = new ProgressLedgerToolHost(seedEntries, onReplaced);
        context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
        if (!progress.GetLedger().IsEmpty && flusher is not null)
            await flusher.FlushAsync(progress.GetLedger());
        var masterBody = prompts.Render(context.MasterSkillName, new Dictionary<string, string>
        {
            ["ProjectContextSection"] = BuildProjectContextSection(context.ProjectContext),
            ["CodingPrinciples"] = context.CodingPrinciples,
            ["CodeMapSection"] = BuildCodeMapSection(context.CodeMap),
            ["RepoNames"] = BuildRepoNamesSection(addressNames),
            ["PlanSection"] = BuildPlanSection(plan),
            ["RunRecordDir"] = runRecordDir,
            // p0258: the master must iterate when its own build/tests come back
            // red (fix the code or the now-stale test, re-run) instead of stopping
            // at the first failure — bounded by this config value (agent.max_fix_
            // iterations, default 3) so a hopeless loop still ends.
            ["MaxFixIterations"] = context.AgentConfig.MaxFixIterations.ToString(),
            // p0328: the ratified acceptance contract. Masters that predate the
            // token simply never contain the placeholder — Render's replace is a
            // no-op then, so old skills pins keep working unchanged.
            ["ExpectationSection"] = Expectations.ExpectationPromptSection.Build(context.Pipeline),
            // p0341: the seeded checklist, so the master opens on it. Masters without
            // the placeholder (older pins) simply never render it — Render is a no-op.
            ["ProgressLedgerSection"] = progress.GetLedger().IsEmpty
                ? string.Empty
                : ProgressLedgerRenderer.Render(progress.GetLedger()),
        });

        logger.LogInformation(
            "Running master skill '{Skill}' for repo {Repo}",
            context.MasterSkillName, context.Repository.LocalPath);
        var runCommandTimeout = context.Pipeline.TryGet<int>(ContextKeys.RunCommandTimeoutSeconds, out var rct)
            ? rct : (int?)null;
        // p0258: pass the logger so the master's file tool calls are visible
        // (`tool_call: WriteFile path=… bytes=…`). Without it the ToolHost was
        // constructed logger-less and we were BLIND to what the master actually
        // wrote — masking the "recorded N files changed but git diff is empty"
        // root cause (no real working-tree change vs wrong path vs no-op edit).
        var fs = new FilesystemToolHost(
            sandboxes, defaultKey, context.Repository.LocalPath,
            runCommandTimeoutSeconds: runCommandTimeout, keyToRepo: keyToRepo, logger: logger);
        var log = new LogDecisionToolHost(decisionLogger, context.Repository.LocalPath);
        // p0315b: the dialogue job id (spec-dialog: the session id) makes ask_human
        // live — questions publish on job:{id}:out and the thread's answers come
        // back on job:{id}:in. Absent (run jobs today) → the tool reports itself
        // unconfigured exactly as before.
        var dialogueJobId = context.Pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var djid)
            && !string.IsNullOrEmpty(djid) ? djid : null;
        // p0315d: a phase-execution run has no live dialogue transport (ephemeral
        // container, ticket-triggered) — ask_human captures the question instead;
        // MasterOpenQuestions posts + parks it after the loop.
        var ticketClarifications = isPhaseExecution ? new TicketClarificationToolHost() : null;
        IToolHost human = ticketClarifications is not null
            ? ticketClarifications
            : new HumanToolHost(dialogueTransport, dialogueJobId);
        var credentials = new GetArtifactCredentialsToolHost(config.Registries);
        // p0341c: constrain write_context_yaml's context_name to the DISCOVERED contexts
        // per repo (from ScopeRepos' RemoteContextInventory) so the model can't author a
        // stray 'default' when discovery already resolved e.g. [api, ...].
        var discoveredContexts = BuildDiscoveredContexts(context.Pipeline);
        var writeDefaultRepoName = keyToRepo is not null
            && keyToRepo.TryGetValue(defaultKey, out var drn) && !string.IsNullOrEmpty(drn)
            ? drn : defaultKey;
        var writeContextYaml = new WriteContextYamlToolHost(
            sandboxes, defaultKey, contextYamlSerializer, discoveredContexts, writeDefaultRepoName);

        // p0356: the probed toolchain inventory enters the CODING master's system
        // prompt as a capability statement — per-run stable, so the automatic
        // prompt-cache anchoring is unaffected. Scan masters review read-only and
        // spec-dialog turns run no commands; neither is probed.
        if (!isScanMaster && !isSpecDialog)
        {
            var toolchainSection = await toolchainProbe.ProbeAsync(sandboxes, keyToRepo, cancellationToken);
            if (!string.IsNullOrEmpty(toolchainSection)) masterBody += "\n\n" + toolchainSection;
        }

        // Every master surface gets web_fetch — a read-only GET of a public URL that
        // mutates nothing, so even the read-only scan surface carries it safely.
        var web = webToolHost;

        // p0317: the whole ticket reaches the master — conversation (delimited),
        // materialized documents + binary listing, and image content parts when
        // the model is vision-capable ("N images, not viewable" note otherwise).
        // A spec-dialog turn has no ticket, so it composes nothing; a phase-
        // execution run gets the SAME extras as the coding path — the hydrated
        // comment thread is exactly what a re-triggered run parked on a
        // clarification needs (closes the p0315d parked-while-answered residual).
        var repoPrefix = addressNames.Count > 1 && keyToRepo is not null
            && keyToRepo.TryGetValue(defaultKey, out var defaultRepoName)
            ? $"{defaultRepoName}/"
            : string.Empty;
        var extras = isSpecDialog
            ? (Conversation: string.Empty, Attachments: string.Empty,
                ImageParts: (IReadOnlyList<AIContent>)[])
            : await ComposeTicketExtrasAsync(
                context, sandboxes[defaultKey], runRecordDir, repoPrefix, isScanMaster, cancellationToken);

        var userPrompt = isSpecDialog
            ? specDialogPromptFactory.Build(context.Pipeline)
            : isPhaseExecution
                ? phasePromptFactory.Build(
                    context.Pipeline,
                    ticket ?? throw new InvalidOperationException(
                        "Phase-execution run has no ticket — FetchTicket must run before the master."),
                    context.Repository, addressNames,
                    extras.Conversation, extras.Attachments)
                : isScanMaster
                    ? scanPromptFactory.Build(context.Pipeline, context.Repository, addressNames)
                    : BuildUserPrompt(ticket, context.Repository, addressNames,
                        extras.Conversation, extras.Attachments);

        // p0341c: the shared cost tracker + the open-loop governor hooks (within-pass
        // money fence + periodic ledger-reminder injection). Built once; reused across
        // every pass — the estimator accumulates all master iterations, the fence compares
        // start-of-master spend + that estimate against the effective cap. Only the coding
        // master (read/write, not scan/spec-dialog) gets the hooks + the large ceiling.
        var costTracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        var masterHooks = isScanMaster || isSpecDialog
            ? null
            : BuildMasterLoopHooks(context, costTracker, () => progress.GetLedger(), log);
        var iterationCeiling = isScanMaster || isSpecDialog
            ? (int?)null
            : context.AgentConfig.MaxMasterLoopIterations;

        var request = new AgenticLoopRequest(
            AgentConfig: context.AgentConfig,
            TaskType: TaskType.Primary,
            SystemPrompt: masterBody,
            UserPrompt: userPrompt,
            Tools: ComposeMasterTools(isScanMaster, isSpecDialog, fs, log, human, credentials, writeContextYaml, web, progress, context),
            UserImageParts: extras.ImageParts,
            MaxIterations: iterationCeiling,
            MasterLoopHooks: masterHooks);

        AgenticLoopResult loopResult;
        try
        {
            loopResult = await loopRunner.RunAsync(request, cancellationToken);
        }
        catch (MasterBudgetExhaustedException budgetEx)
        {
            // p0341c: the within-pass money fence tripped — stop cleanly, ship the partial
            // work + the current ledger, and record an honest cost-cap-exhausted outcome
            // (the pipeline finalizes with a record/partial PR). Never a laundered green.
            context.Pipeline.Set(ContextKeys.CodeChanges, fs.GetChanges());
            context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
            var partial = log.GetDecisions();
            if (partial.Count > 0) context.Pipeline.AppendDecisions(partial);
            logger.LogWarning(
                "Master '{Skill}' stopped on the per-pipeline cost budget: {Reason}",
                context.MasterSkillName, budgetEx.Message);
            return CommandResult.Fail(budgetEx.Message);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // p0237: the master loop failed WITHOUT an operator/watchdog cancel
            // (that leaves cancellationToken cancelled — let it propagate to the
            // pipeline-level p0232 handler). The common case is an LLM-layer
            // NetworkTimeout surfacing as a cancellation. Preserve whatever the
            // agent already wrote + decided, then FAIL with a clear reason so the
            // pipeline finalizes (records result.md + opens a record/partial PR)
            // instead of a bare ".NET "A task was canceled.".
            context.Pipeline.Set(ContextKeys.CodeChanges, fs.GetChanges());
            context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
            var partialDecisions = log.GetDecisions();
            if (partialDecisions.Count > 0) context.Pipeline.AppendDecisions(partialDecisions);
            var reason = DescribeMasterFailure(ex);
            logger.LogWarning(ex, "Master skill '{Skill}' failed: {Reason}", context.MasterSkillName, reason);
            return CommandResult.Fail(reason);
        }

        // p0341e: the coding master's spend is now recorded PER ITERATION by the governor hook
        // (BuildMasterLoopHooks → RecordIterationUsage feeds the shared tracker), so tracking the
        // final aggregate here would DOUBLE-count it — and would still be lost on a throwing pass.
        // Track the final response ONLY on the paths that have no governor hooks (scan / spec-
        // dialog masters), where the loop is a single aggregate and never re-driven.
        void TrackMasterResponse(ChatResponse response)
        {
            if (masterHooks is null) costTracker.Track(response);
        }

        TrackMasterResponse(loopResult.Response);

        // p0315d: the master asked mid-run — pause the run instead of nudging it
        // on. Publish the partial work + the question; MasterOpenQuestions posts
        // it to the ticket and parks, the executor short-circuits the rest.
        if (ticketClarifications?.Captured is { } masterQuestion)
        {
            context.Pipeline.Set(ContextKeys.CodeChanges, fs.GetChanges());
            var partial = log.GetDecisions();
            if (partial.Count > 0) context.Pipeline.AppendDecisions(partial);
            context.Pipeline.Set<IReadOnlyList<Domain.Entities.PlanOpenQuestion>>(
                ContextKeys.MasterOpenQuestions, [masterQuestion]);
            logger.LogInformation(
                "Master '{Skill}' asked for clarification mid-run — pausing for the ticket answer",
                context.MasterSkillName);
            return CommandResult.Ok("awaiting_user_input: master asked for clarification mid-run");
        }

        // p0279: a scan/review master that barely read the source did a shallow pass —
        // re-prompt ONCE to inventory the full surface and review each area, reading its
        // code. Coverage signal = distinct source reads (FilesystemToolHost.ReadPaths);
        // bounded, scan-only. Prevents a near-empty pass; it does not guarantee every
        // class is checked (model concern). The same fs accumulates the deeper reads.
        if (isScanMaster && fs.ReadPaths.Count < context.AgentConfig.ScanMinSourceReads)
        {
            logger.LogWarning(
                "Scan master '{Skill}' read only {Count} source file(s) (< floor {Floor}) — re-prompting once for deeper coverage",
                context.MasterSkillName, fs.ReadPaths.Count, context.AgentConfig.ScanMinSourceReads);
            try
            {
                var deeper = await loopRunner.RunAsync(
                    request with { UserPrompt = scanPromptFactory.BuildCoverageNudge(userPrompt) }, cancellationToken);
                TrackMasterResponse(deeper.Response);
                loopResult = deeper; // the deeper pass re-emits the complete observation array
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Coverage re-drive failed for scan master '{Skill}'", context.MasterSkillName);
            }
        }

        // p0315b/p0315e: a spec-dialog reply's typed terminal outcome (answer /
        // bug / phase / epic) must resolve and validate BEFORE it is shown.
        // Invalid → re-prompt the master ONCE with the exact error; still
        // invalid → replace the reply with an honest failure notice. The raw
        // invalid output never reaches the thread.
        if (isSpecDialog)
            loopResult = await GateSpecOutcomeAsync(
                context.Pipeline, request, userPrompt, loopResult, costTracker, cancellationToken);

        var changes = fs.GetChanges();

        // p0255: the master sometimes writes a plan/decisions but applies NO source
        // edits — the recurring "investigated, planned, then stopped" run that ships
        // nothing (a correct plan.md, zero source writes). When code is expected and
        // only run-record artifacts were written, re-prompt the master ONCE with a
        // focused "apply your plan now" instruction: a bounded second shot that
        // turns a wasted no-edit run into real work. The git-authoritative keystone
        // (CommitAndPR) still gates the final outcome either way.
        if (ShouldDriveApply(pipelineName, changes))
        {
            logger.LogWarning(
                "Master '{Skill}' wrote a plan but edited no source — re-prompting once to apply it",
                context.MasterSkillName);
            try
            {
                var applyResult = await loopRunner.RunAsync(
                    request with { UserPrompt = BuildApplyNudge(userPrompt, progress.GetLedger()) }, cancellationToken);
                TrackMasterResponse(applyResult.Response);
                loopResult = applyResult; // verdict + duration come from the apply pass
                changes = fs.GetChanges();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The apply retry failed on its own — keep the first pass; the
                // keystone records the run as FAILED with a concrete reason.
                logger.LogWarning(ex, "Apply re-prompt failed for master '{Skill}'", context.MasterSkillName);
            }
        }

        var decisions = log.GetDecisions();

        context.Pipeline.Set(ContextKeys.CodeChanges, changes);
        // p0341: the final ledger for the done-status diagnostic (WriteRunResult) + result.md.
        context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
        context.Pipeline.Set(ContextKeys.RunDurationSeconds, (int)loopResult.Duration.TotalSeconds);

        // p0267: publish the master's final answer + skill name so a downstream
        // findings-scrape (CollectMasterFindings on the api-security path) can route
        // the master's TRIAGED observation-array into SkillObservations. Unconditional
        // and content-agnostic — the coding path simply never runs a consumer.
        context.Pipeline.Set(ContextKeys.MasterAnswer, loopResult.Response.Text ?? string.Empty);
        context.Pipeline.Set(ContextKeys.MasterSkillName, context.MasterSkillName);
        // p0279: publish the scan master's read-set (post re-drive) so the findings scrape
        // can downgrade an analyzed_from_source claim on a file the master never read.
        context.Pipeline.Set(ContextKeys.MasterReadPaths, fs.ReadPaths.ToList());

        // p0241: parse the master's structured verification verdict from its final
        // answer and publish it for the keystone. The model owns running the
        // build/tests and declaring the result; the framework only enforces that
        // an unverified/red run is never reported as success (CommitAndPRHandler).
        var verification = MasterVerificationParser.TryParse(loopResult.Response.Text);

        // p0263: the master changed source but emitted no parseable Phase 4 verdict — a
        // model-fitness miss (gpt-4.1-class models do the work yet skip the closing
        // artifact, sinking the run at the keystone). Sibling to the p0255 apply-drive:
        // when a verdict is EXPECTED (a green-tests pipeline) and none was parsed,
        // re-prompt the master ONCE to verify (no further edits) and emit ONLY the
        // verdict, then re-parse. Bounded; the git + verdict keystone still gates.
        if (ShouldNudgeForVerdict(pipelineName, verification))
        {
            logger.LogWarning(
                "Master '{Skill}' changed code but emitted no verdict — re-prompting once for it",
                context.MasterSkillName);
            try
            {
                var verdictResult = await loopRunner.RunAsync(
                    request with { UserPrompt = BuildVerdictNudge(userPrompt, progress.GetLedger()) }, cancellationToken);
                TrackMasterResponse(verdictResult.Response);
                verification = MasterVerificationParser.TryParse(verdictResult.Response.Text);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Verdict re-prompt failed for master '{Skill}'", context.MasterSkillName);
            }
        }

        // p0341c: the OPEN loop's re-engagement. A model that quit early while budget AND
        // actionable ledger steps remain is driven on — bounded by MONEY + FORWARD PROGRESS,
        // never a fixed re-drive count. Each pass resumes WARM: the nudge carries the current
        // ledger (checklist) AND a working-state block (decisions + last build/test — the
        // continuity). Stop on: drained ledger, honest RED, budget exhausted, a zero-forward-
        // progress pass, or a parked operator question (which short-circuits the whole run).
        (loopResult, changes, verification) = await ReengageWhileProductiveAsync(
            context, request, userPrompt, pipelineName, progress, fs, log,
            costTracker, TrackMasterResponse, ticketClarifications, loopResult, changes, verification,
            cancellationToken);
        if (ticketClarifications?.Captured is { } reengageQuestion)
        {
            context.Pipeline.Set(ContextKeys.CodeChanges, changes);
            var partialQ = log.GetDecisions();
            if (partialQ.Count > 0) context.Pipeline.AppendDecisions(partialQ);
            context.Pipeline.Set<IReadOnlyList<Domain.Entities.PlanOpenQuestion>>(
                ContextKeys.MasterOpenQuestions, [reengageQuestion]);
            context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
            logger.LogInformation(
                "Master '{Skill}' asked for clarification during re-engagement — pausing for the ticket answer",
                context.MasterSkillName);
            return CommandResult.Ok("awaiting_user_input: master asked for clarification during re-engagement");
        }
        // Re-publish the refreshed changes + ledger so the keystone + result.md see the
        // final open-loop state, not the first pass's.
        context.Pipeline.Set(ContextKeys.CodeChanges, changes);
        context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());

        if (verification is not null)
        {
            context.Pipeline.Set(ContextKeys.MasterVerification, verification);
            logger.LogInformation(
                "Master '{Skill}' verdict: {Status} (build {BuildRan}/{BuildPassed}, tests {TestsRan}/{TestsPassed})",
                context.MasterSkillName, verification.Status,
                verification.BuildRan, verification.BuildPassed,
                verification.TestsRan, verification.TestsPassed);
        }
        else if (!isScanMaster && !isSpecDialog)
        {
            // p0278: a scan/review master never emits a build/test verdict — only a
            // coding master is expected to, so don't warn about its absence on a scan.
            // p0315b: same for the design-partner conversation — it ships no code.
            logger.LogWarning(
                "Master '{Skill}' emitted no parseable verification verdict", context.MasterSkillName);
        }

        if (decisions.Count > 0)
        {
            context.Pipeline.AppendDecisions(decisions);
        }

        LogContextCostTelemetry(context, costTracker, progress.GetLedger());
        logger.LogInformation(
            "Master skill '{Skill}' completed: {Count} files changed, {Decisions} decisions",
            context.MasterSkillName, changes.Count, decisions.Count);

        return CommandResult.Ok($"Master '{context.MasterSkillName}' completed: {changes.Count} files changed");
    }

    // p0356: the scaling signal — flat tokens-per-done-item is healthy on an
    // overlay run; an upward trend means the conventions digest is missing
    // something. Cached share stuck at 0% on a caching-capable model is the
    // p0323 alarm.
    private void LogContextCostTelemetry(
        AgenticMasterContext context, PipelineCostTracker costTracker, ProgressLedger ledger)
    {
        var report = Metrics.ContextCostTelemetry.Compute(
            costTracker.TotalTokens, costTracker.TotalCacheReadTokens, ledger);
        logger.LogInformation(
            "Context cost for master '{Skill}': {TotalTokens} tokens total, cached share {CachedShare:P0}, "
            + "{DoneItems} ledger item(s) done, tokens/item {TokensPerItem}",
            context.MasterSkillName, report.TotalTokens, report.CachedShare, report.DoneItems,
            report.TokensPerDoneItem?.ToString() ?? "n/a");
    }

    // p0356: the same-ticket RESUME seed — the latest prior run's persisted
    // ledger (flushed mid-run, so a reaped run left one behind), gated in
    // PriorRunLedgerSeeder. Read failures degrade to the empty seed; resume is
    // an affordance, never a blocker.
    private async Task<IReadOnlyList<ProgressLedgerEntry>> SeedFromPriorRunAsync(
        Ticket ticket, CancellationToken cancellationToken)
    {
        try
        {
            var prior = await priorRunLedgerReader.ReadLatestForTicketAsync(ticket.Id.Value, cancellationToken);
            var seed = PriorRunLedgerSeeder.Seed(prior, DateTimeOffset.UtcNow);
            if (seed.Count > 0)
                logger.LogInformation(
                    "Seeded the progress ledger from prior run {PriorRunId} ({Count} item(s), same-ticket resume)",
                    prior!.RunId, seed.Count);
            return seed;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Prior-run ledger read failed — starting with an empty ledger");
            return Array.Empty<ProgressLedgerEntry>();
        }
    }

    // p0315b/p0315e: resolve the spec-dialog reply's typed terminal outcome and
    // publish it for CollectSpecDialogReply; on failure re-prompt the master
    // ONCE with the exact error (same pattern as the p0255/p0263 nudges), and
    // on a second failure replace the reply with an honest notice — the raw
    // invalid output is never surfaced.
    private async Task<AgenticLoopResult> GateSpecOutcomeAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt,
        AgenticLoopResult loopResult, PipelineCostTracker costTracker, CancellationToken ct)
    {
        var resolution = outcomeResolver.Resolve(loopResult.Response.Text ?? string.Empty);
        if (resolution is OutcomeResolved first)
            return PublishOutcome(pipeline, first.Proposal, loopResult);
        var invalid = (OutcomeInvalid)resolution;

        logger.LogWarning(
            "Design-partner terminal outcome failed validation — re-prompting once: {Error}",
            invalid.Error);
        AgenticLoopResult retry;
        try
        {
            retry = await loopRunner.RunAsync(
                request with { UserPrompt = specDialogPromptFactory.BuildOutcomeFixNudge(userPrompt, invalid.Error) },
                ct);
            costTracker.Track(retry.Response);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Outcome-fix re-prompt failed");
            return FailOutcome(pipeline, loopResult, invalid.Error);
        }

        var retryResolution = outcomeResolver.Resolve(retry.Response.Text ?? string.Empty);
        if (retryResolution is OutcomeResolved second)
            return PublishOutcome(pipeline, second.Proposal, retry);

        var stillInvalid = (OutcomeInvalid)retryResolution;
        logger.LogWarning(
            "Design-partner terminal outcome still invalid after re-prompt: {Error}", stillInvalid.Error);
        return FailOutcome(pipeline, retry, stillInvalid.Error);
    }

    private static AgenticLoopResult PublishOutcome(
        PipelineContext pipeline, OutcomeProposal proposal, AgenticLoopResult result)
    {
        pipeline.Set(ContextKeys.SpecDialogOutcome, proposal);
        return result;
    }

    // A twice-invalid outcome degrades to an honest answer: the notice is the
    // reply and nothing is proposed for routing.
    private static AgenticLoopResult FailOutcome(
        PipelineContext pipeline, AgenticLoopResult result, string error)
    {
        pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)new AnswerOutcome());
        return WithReplyText(result, OutcomeFailureNotice(error));
    }

    private static AgenticLoopResult WithReplyText(AgenticLoopResult result, string text) =>
        result with { Response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text)) };

    private static string OutcomeFailureNotice(string error) =>
        "I proposed an outcome for this design turn, but it did not pass validation "
        + $"({error}), so I am not showing it. Refine the requirements or ask me to "
        + "draft again.";

    // p0280: the master surface = its base surface (read-only Review for a scan master,
    // read/write for a coding master) PLUS spawn_agents + read_sub_agent_observations when
    // sub-agents are enabled. Children SHARE this fs (so their reads/writes aggregate into
    // the master's read-set + changes) and get the same base surface — never spawn_agents.
    // p0315b: the spec-dialog surface is content-reads + ask_human only, no sub-agents —
    // a conversation turn neither writes nor delegates.
    private IList<AITool> ComposeMasterTools(
        bool isScanMaster, bool isSpecDialog, FilesystemToolHost fs, LogDecisionToolHost log, IToolHost human,
        GetArtifactCredentialsToolHost credentials, WriteContextYamlToolHost writeContextYaml,
        WebToolHost? web, ProgressLedgerToolHost progress, AgenticMasterContext context)
    {
        if (isSpecDialog) return AgenticToolSurface.SpecDialog(fs, human, web);
        IList<AITool> BaseSurface() => isScanMaster
            ? AgenticToolSurface.Review(fs, log, web)
            : AgenticToolSurface.ReadWriteWithHuman(
                fs, log, human, web: web, credentials: credentials, writeContextYaml: writeContextYaml);

        var master = BaseSurface();
        // p0331: coding masters get the ensure_repo_sandbox escalation valve — the
        // counterpart to ScopeRepos' conservative narrowing. Scan masters read
        // everything anyway (full scope, no narrowing) and must not spawn.
        // p0341: coding masters also get update_progress (the durable ledger); scan /
        // spec-dialog surfaces never do — a read-only review keeps no checklist.
        if (!isScanMaster)
            master = master
                .Concat(ensureRepoSandboxFactory.Create(context.Pipeline, fs, logger).GetTools(null, null))
                .Concat(progress.GetTools(null, null))
                .ToList();
        if (loopLimits.MaxSubAgentsPerRun <= 0) return master;

        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var rid) && rid is not null ? rid : "run";
        var sandboxes = context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var subCtx = new SubAgentContext(
            context.Pipeline, sandboxes, PipelineCostTracker.GetOrCreate(context.Pipeline), runId,
            ChildTools: BaseSurface().ToList(), AnswerStore: childAnswerStore, Budget: subAgentBudget,
            AgentConfig: context.AgentConfig);
        var spawn = new SpawnAgentToolHost(subAgentRunner, subAgentBudget, subAgentNameValidator, decisionLogger, subCtx);
        var readObs = new ReadSubAgentObservationsToolHost(childAnswerStore);
        return master.Concat(spawn.GetTools(null, null)).Concat(readObs.GetTools(null, null)).ToList();
    }

    // p0255: re-prompt the master to APPLY when the run expects edited source
    // (fix-bug / add-feature; not mad-discussion / scans) but it wrote only
    // run-record artifacts — a plan with zero source edits. Pure + testable.
    internal static bool ShouldDriveApply(string? pipelineName, IReadOnlyList<CodeChange> changes) =>
        !string.IsNullOrEmpty(pipelineName)
        && PipelinePresets.ExpectsCodeChanges(pipelineName)
        && !changes.Any(c => !RunRecordPaths.IsRunRecordPath(c.Path.ToString()));

    // p0263: re-prompt the master to EMIT ITS VERDICT when it changed source but
    // emitted no parseable Phase 4 verdict and a verdict is expected (a green-tests
    // pipeline). Model-fitness salvage — the skill instructs Phase 4; some models skip
    // the closing artifact. Pure + testable. Mirrors ShouldDriveApply.
    internal static bool ShouldNudgeForVerdict(string? pipelineName, MasterVerification? verification) =>
        verification is null
        && !string.IsNullOrEmpty(pipelineName)
        && PipelinePresets.ExpectsGreenTests(pipelineName);

    // p0263: the focused second-shot prompt when the master edited source but emitted
    // no verdict — verify only (no further edits) and emit ONLY the verdict block.
    private static string BuildVerdictNudge(string originalUserPrompt, ProgressLedger ledger) =>
        "Your previous pass changed source but did NOT emit the required Phase 4 verdict, "
        + "so the run cannot be reported. Do NOT make further code changes now. Build the "
        + "project and run the automated tests the way the repository defines them, then emit "
        + "ONLY your final fenced ```verdict block reflecting the real build/test outcome "
        + "(status: green | no-tests | failed). Nothing before or after the block.\n\n"
        + LedgerNudgeSection(ledger)
        + "Original task:\n" + originalUserPrompt;

    // p0255: the focused second-shot prompt when the master planned but edited
    // nothing — the plan is not the deliverable, the edited source is.
    private static string BuildApplyNudge(string originalUserPrompt, ProgressLedger ledger) =>
        "You wrote a plan but have NOT edited any source file yet. The plan is not the "
        + "deliverable — the edited source is. Apply your plan NOW: make the edits with "
        + "edit / multi_edit / write_file (repo-prefixed paths), then build, run the tests, "
        + "and emit your verdict. Do not stop until at least one SOURCE file is changed, or "
        + "you report a concrete blocker explaining why no edit was possible.\n\n"
        + LedgerNudgeSection(ledger)
        + "Original task:\n" + originalUserPrompt;

    // p0341: a re-drive starts a fresh loop, so carry the ledger forward from
    // PipelineContext (done vs remaining) — the salvage pass resumes the checklist
    // instead of restarting blind. Empty ledger (no plan) contributes nothing.
    private static string LedgerNudgeSection(ProgressLedger ledger) =>
        ledger.IsEmpty ? string.Empty : ProgressLedgerRenderer.Render(ledger) + "\n\n";

    // p0341c: an absolute anti-hang net on re-engagement passes for the fail-open case
    // (no cost cap configured). It is NOT the control — money + forward progress are; this
    // only prevents a pathological spin when the budget is disabled.
    private const int ReengageHardSafetyCap = 50;

    // p0341c: the open-loop re-engagement driver. Loops WHILE ShouldReengage holds AND the
    // previous pass made MEANINGFUL forward progress (a newly-done step or a now-passing
    // verdict — never a bare edit), re-running the loop with a warm nudge (current ledger +
    // working-state block). Stops on drained ledger, honest RED, budget exhausted, a
    // zero-forward-progress pass, a parked operator question, or the hard safety net.
    private async Task<(AgenticLoopResult LoopResult, IReadOnlyList<CodeChange> Changes, MasterVerification? Verification)>
        ReengageWhileProductiveAsync(
            AgenticMasterContext context, AgenticLoopRequest request, string userPrompt,
            string? pipelineName, ProgressLedgerToolHost progress, FilesystemToolHost fs,
            LogDecisionToolHost log, PipelineCostTracker costTracker,
            Action<ChatResponse> trackMasterResponse,
            TicketClarificationToolHost? ticketClarifications,
            AgenticLoopResult loopResult, IReadOnlyList<CodeChange> changes,
            MasterVerification? verification, CancellationToken cancellationToken)
    {
        var ratifiedCriteria = RatifiedCriteria(context.Pipeline);
        for (var pass = 0; pass < ReengageHardSafetyCap; pass++)
        {
            if (!ShouldReengage(
                    pipelineName, progress.GetLedger(), verification,
                    costTracker.IsBudgetExhausted, ratifiedCriteria, changes))
                break;
            if (ticketClarifications?.Captured is not null)
                break; // an operator question short-circuits — the caller parks the run

            var doneBefore = CountDone(progress.GetLedger());
            var changesBefore = changes.Count;
            var verificationBefore = verification;
            var passThrew = false;

            logger.LogInformation(
                "Master '{Skill}' re-engaging the open loop — {Remaining} actionable step(s) remain, budget OK",
                context.MasterSkillName, progress.GetLedger().ActionablePending.Count);
            try
            {
                var reengaged = await loopRunner.RunAsync(
                    request with
                    {
                        UserPrompt = BuildReengageNudge(userPrompt, progress.GetLedger(), log.GetDecisions(), verification),
                    },
                    cancellationToken);
                // p0341e: no-op for the coding master (per-iteration governor hook already
                // recorded this pass's spend); the shared helper keeps the gating in one place.
                trackMasterResponse(reengaged.Response);
                loopResult = reengaged;
                changes = fs.GetChanges();
                var reparsed = MasterVerificationParser.TryParse(reengaged.Response.Text);
                if (reparsed is not null) verification = reparsed;
            }
            catch (MasterBudgetExhaustedException)
            {
                logger.LogWarning(
                    "Master '{Skill}' hit the cost budget mid re-engagement — stopping (partial work preserved)",
                    context.MasterSkillName);
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A crashed/timeout pass is RECOVERY, not zero-progress — keep the prior
                // state and let the next iteration re-decide against the budget.
                logger.LogWarning(ex, "Re-engagement pass failed for master '{Skill}'", context.MasterSkillName);
                passThrew = true;
            }

            if (ticketClarifications?.Captured is not null)
                break; // asked during the pass — caller parks

            if (!MadeForwardProgress(
                    doneBefore, CountDone(progress.GetLedger()), verificationBefore, verification, passThrew))
            {
                logger.LogInformation(
                    "Master '{Skill}' re-engagement pass made no forward progress (no newly-done step, no now-passing "
                    + "verdict) — stopping the open loop", context.MasterSkillName);
                break;
            }
            _ = changesBefore; // retained for readability of the progress semantics
        }

        return (loopResult, changes, verification);
    }

    // p0341c/p0341e: the re-engagement predicate — pure + testable, mirroring ShouldDriveApply /
    // ShouldNudgeForVerdict. Re-engages the open loop while the run is OBJECTIVELY incomplete —
    // not merely while the MODEL still reports pending steps. A model that drains the ledger by
    // marking steps done WITHOUT doing them (or a plan that under-seeds the repo) previously
    // defeated re-engagement: HasActionablePending went false and the loop quit early, leaving
    // the keystone to catch the lie only at the very end. Now three signals, first two OBJECTIVE:
    //   (1) the model's own checklist still has actionable steps (the original signal), OR
    //   (2) a DONE-marked step's declared target is absent from the actual diff (marking-without-
    //       doing — the diff is unfakeable), OR
    //   (3) the ratified acceptance contract is not yet objectively satisfied (build/tests green
    //       AND every criterion met/justified) — a drained ledger over an unmet contract is not
    //       a real completion.
    // Honest RED is respected only when the ledger is drained (p0363) — RED with open
    // actionable steps is a mid-work status report and gets re-driven. Budget exhaustion
    // always stops. Bounded by the caller's forward-progress gate + the hard safety cap —
    // a red re-drive that moves nothing ends the loop after one pass.
    internal static bool ShouldReengage(
        string? pipelineName, ProgressLedger ledger, MasterVerification? verification,
        bool budgetExhausted, IReadOnlyList<string> ratifiedCriteria, IReadOnlyList<CodeChange> changes)
    {
        if (string.IsNullOrEmpty(pipelineName) || !PipelinePresets.ExpectsCodeChanges(pipelineName))
            return false;
        if (budgetExhausted) return false;
        // p0363: honest RED is terminal ONLY when the model has nothing actionable left.
        // A RED verdict WITH open checklist items ("Build solutions and fix compile
        // issues" marked NOW) is a status report mid-work, not a verdict of
        // impossibility — the observed failure mode: the model runs its verification,
        // sees the red build, emits FAILED and stops with $43 of budget and 80 minutes
        // of wall-time left. Re-drive it; the caller's forward-progress gate still ends
        // the loop after one red pass that moves nothing, so persistence stays bounded
        // and justified surrender (RED + drained ledger) is still respected.
        if (verification?.Status == VerificationStatus.Failed && !ledger.HasActionablePending)
            return false;

        if (ledger.HasActionablePending) return true;
        if (ProgressLedgerCoverage.UnbackedDoneSteps(ledger, changes).Count > 0) return true;
        if (ratifiedCriteria.Count > 0
            && !AcceptanceObjectivelySatisfied(verification, ratifiedCriteria.Count))
            return true;
        return false;
    }

    // p0341e: the objective acceptance gate mirrored from RunOutcomeKeystone.EvaluateAcceptance
    // (the single definition of done). The contract is satisfied ONLY when the build/tests are
    // green (or genuinely test-less) AND every ratified criterion has a reported disposition that
    // is Met or justified not-applicable. A missing verdict, a non-green status, or any unmet /
    // missing disposition => not satisfied. Pure + testable.
    internal static bool AcceptanceObjectivelySatisfied(MasterVerification? verification, int criteriaCount)
    {
        if (criteriaCount == 0) return true;
        if (verification is null) return false;
        if (verification.Status is not (VerificationStatus.Green or VerificationStatus.NoTests))
            return false;
        var dispositions = verification.AcceptanceDispositions;
        if (dispositions is null || dispositions.Count < criteriaCount) return false;
        for (var i = 0; i < criteriaCount; i++)
        {
            var d = dispositions[i];
            if (d.Status == AcceptanceStatus.Met) continue;
            if (d.Status == AcceptanceStatus.NotApplicable && !string.IsNullOrWhiteSpace(d.Evidence)) continue;
            return false;
        }
        return true;
    }

    // p0341e: the ratified acceptance criteria for this run (empty when nothing was negotiated —
    // fix-bug self-planning, ticketless runs). Same source the keystone reads.
    private static IReadOnlyList<string> RatifiedCriteria(PipelineContext pipeline) =>
        pipeline.TryGet<Contracts.Expectations.RatifiedExpectation>(
            ContextKeys.RunExpectation, out var exp) && exp is not null
            ? exp.Draft.Expected
            : Array.Empty<string>();

    // p0341c: MEANINGFUL forward progress — a newly-DONE ledger step, or a verdict that now
    // passes (a repo that now builds / tests that now pass). A bare edit that moved neither
    // is NOT progress (a shallow-but-nonzero pass must not count). A pass that ended on an
    // exception/timeout is RECOVERY, not zero-progress. Pure + testable.
    internal static bool MadeForwardProgress(
        int doneStepsBefore, int doneStepsAfter,
        MasterVerification? verificationBefore, MasterVerification? verificationAfter,
        bool passEndedOnException)
    {
        if (passEndedOnException) return true;
        if (doneStepsAfter > doneStepsBefore) return true;
        return NowPasses(verificationAfter) && !NowPasses(verificationBefore);
    }

    private static bool NowPasses(MasterVerification? v) =>
        v?.Status is VerificationStatus.Green or VerificationStatus.NoTests;

    private static int CountDone(ProgressLedger ledger) =>
        ledger.Entries.Count(e => e.Status == Contracts.Progress.ProgressStatus.Done);

    // p0341c: the WARM re-engagement nudge — the current ledger (the checklist / coverage)
    // PLUS a working-state block (decisions so far + last build/test tail — the continuity),
    // so a resumed pass carries WHAT WAS LEARNED, not only WHAT REMAINS.
    private static string BuildReengageNudge(
        string originalUserPrompt, ProgressLedger ledger,
        IReadOnlyList<PlanDecision> decisions, MasterVerification? verification) =>
        // p0363: a red verdict with open checklist items gets an explicit persistence
        // lead-in — the failing build IS the current step, not a reason to stop.
        (verification?.Status == VerificationStatus.Failed
            ? "Your last verification came back RED — and your own checklist still has "
              + "actionable steps for exactly that (fixing the build/tests IS the current "
              + "step). Reporting the failure is not completing the step: keep working the "
              + "checklist, and mark steps done as they actually pass. Only stop if you can "
              + "justify concretely why the remaining steps cannot succeed.\n\n"
            : "")
        + "Continue the checklist — these plan steps still remain. You are NOT done until the "
        + "checklist is drained; resume from where you left off, do not restart from scratch. "
        + "If a remaining step needs a decision only the operator can make, use ask_human and "
        + "stop rather than guessing.\n\n"
        + LedgerNudgeSection(ledger)
        + BuildWorkingStateBlock(decisions, verification)
        + "Original task:\n" + originalUserPrompt;

    // p0341c: the continuity carry rendered into a re-engagement pass — decisions committed
    // so far + the last build/test tail. Pure so it is unit-testable in isolation.
    internal static string BuildWorkingStateBlock(
        IReadOnlyList<PlanDecision> decisions, MasterVerification? verification)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Working state (carry this forward)");
        if (decisions is { Count: > 0 })
        {
            sb.AppendLine("Decisions committed so far:");
            foreach (var d in decisions.Take(12))
                sb.AppendLine($"- [{d.Category}] {d.Decision}");
        }
        else
        {
            sb.AppendLine("Decisions committed so far: (none logged yet)");
        }
        var tail = verification?.Summary;
        sb.AppendLine("Last build/test: "
            + (string.IsNullOrWhiteSpace(tail)
                ? $"status {verification?.Status.ToString() ?? "not yet run"}"
                : tail));
        sb.AppendLine();
        return sb.ToString();
    }

    // p0341c/p0359: the in-pass reminder, injected when the ledger went STALE (N
    // iterations without an update_progress call) or on drift. Styled after an
    // interactive harness's todo reminder: gentle, states that restructuring is
    // allowed (the plan may have deviated), and explicitly ignorable when the
    // shown state is still accurate — a nag the model can dismiss beats one it
    // learns to tune out.
    internal static string BuildInPassReminder(ProgressLedger ledger)
    {
        if (ledger.IsEmpty)
            return "<system-reminder>\n"
                + "The progress ledger is empty and the update_progress tool has not been used "
                + "recently. If you are doing multi-step work, seed the checklist from your plan "
                + "now — it is your durable memory across this run. If the task is genuinely "
                + "trivial, ignore this reminder.\n"
                + "</system-reminder>";
        if (ledger.ActionablePending.Count == 0)
            return "<system-reminder>\n"
                + "Every step in the progress ledger is marked done. If the work is truly "
                + "complete, verify (build + tests) and emit your verdict. If you are doing "
                + "work the checklist does not cover, add those steps with update_progress — "
                + "the ledger should reflect what you are actually doing.\n"
                + "</system-reminder>";
        return "<system-reminder>\n"
            + "The update_progress tool has not been used recently. If you completed steps, mark "
            + "them done; flip the step you are working on to in_progress. If the plan has "
            + "evolved, restructure the checklist (add, reword, or remove steps — full-state "
            + "replace) so it reflects what you are ACTUALLY doing. Current recorded state:\n"
            + ProgressLedgerRenderer.Render(ledger) + "\n"
            + "If this is still accurate and you are mid-step, ignore this reminder.\n"
            + "</system-reminder>";
    }

    // p0341c: assemble the open-loop governor hooks — the within-pass money fence + the
    // ledger-reminder injection. The fence uses an independent per-pass estimator seeded
    // from the master's start-of-loop spend, so it stays a clean signal separate from the
    // shared tracker (which the handler updates between passes for result.md accuracy).
    private static MasterLoopHooks BuildMasterLoopHooks(
        AgenticMasterContext context, PipelineCostTracker costTracker, Func<ProgressLedger> ledger,
        LogDecisionToolHost log)
    {
        context.Pipeline.TryGet<IModelPricingResolver>("ModelPricingResolver", out var resolver);
        context.Pipeline.TryGet<PricingConfig>("ProjectPricing", out var pricingConfig);
        var cap = context.Pipeline.TryGet<CostCapValues>("PipelineCostCap", out var c) ? c : null;
        var estimator = new PipelineCostTracker(resolver, pricingConfig, null);
        var startUsd = costTracker.EstimateCostUsd();
        var startTokens = costTracker.TotalTokens;
        return new MasterLoopHooks(
            IsBudgetExhausted: cap is null
                ? null
                : () => startUsd + estimator.EstimateCostUsd() > cap.Usd
                    || startTokens + estimator.TotalTokens > cap.Tokens,
            // p0341e: record EACH tool-loop iteration's usage into BOTH the pass-local fence
            // estimator AND the shared per-pipeline tracker — as it happens. This is the fix
            // for the run summary that showed $0.14 while the master truly spent $16.38: the
            // handler previously fed the shared tracker ONLY the FunctionInvokingChatClient's
            // final aggregate via Track(loopResult.Response) AFTER the loop, so a pass that
            // ended by THROWING (the within-pass money fence, or an LLM-layer timeout) dropped
            // its ENTIRE spend from the summary and from IsBudgetExhausted. Feeding per
            // iteration makes the shared tracker exact and exception-proof; the redundant
            // handler-level Track calls for the coding master are dropped to avoid double-count.
            // The fence math is unaffected — it reads the FROZEN startUsd/startTokens plus the
            // independent estimator, never the shared tracker live.
            RecordIterationUsage: response =>
            {
                estimator.Track(response);
                costTracker.Track(response);
            },
            RenderReminder: () => BuildInPassReminder(ledger()),
            ReminderEveryNIterations: context.AgentConfig.LedgerReminderEveryNIterations,
            DriftEditlessIterations: context.AgentConfig.ReminderDriftEditlessIterations,
            // p0341d: the compaction PIN carriers — rendered CURRENT from PipelineContext /
            // the live decision log at compaction time, never a pass-start snapshot. So the
            // continuous pass preserves the THREAD (ledger + working state) as it compacts.
            RenderLedgerForPin: () =>
            {
                var l = ledger();
                return l.IsEmpty ? null : ProgressLedgerRenderer.Render(l);
            },
            RenderWorkingStateForPin: () => BuildWorkingStateBlock(log.GetDecisions(), null),
            Compaction: context.AgentConfig.Compaction);
    }

    // p0237: turn the master loop's exception into an operator-actionable reason.
    // An OperationCanceledException here (the run token was NOT cancelled — see
    // the caller's `when` guard) is an internal LLM-layer timeout, not a real
    // cancel; name the lever. Everything else carries its type + message.
    private static string DescribeMasterFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is OperationCanceledException)
                return "The coding agent was cut off by an internal timeout (not an "
                    + "operator cancel). If a build/test command was running it likely "
                    + "exceeded sandbox.run_command_timeout_seconds; if an LLM call "
                    + "stalled, raise the agent's network_timeout_seconds (default 300s). "
                    + "Partial work, if any, was preserved.";
        }
        return $"The coding agent failed: {ex.GetType().Name}: {ex.Message}";
    }

    // p0341c: project the RemoteContextInventory (repo name → discovered contexts) into a
    // repo-name → context-name-list map for the write_context_yaml guard. Absent inventory
    // (bootstrap runs, --context override) => null, so the guard is a no-op.
    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? BuildDiscoveredContexts(
        PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.RemoteContextInventory, out var inv) || inv is null || inv.Count == 0)
            return null;
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (repoName, discoveries) in inv)
            map[repoName] = discoveries
                .Select(d => d.ContextName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        return map;
    }

    private static string BuildProjectContextSection(string? projectContext) =>
        string.IsNullOrWhiteSpace(projectContext)
            ? string.Empty
            : $"## Project Context\n{projectContext}\n";

    private static string BuildCodeMapSection(string? codeMap) =>
        string.IsNullOrWhiteSpace(codeMap)
            ? string.Empty
            : $"## Code Map\n{codeMap}\n";

    // p0276: render the operator-approved plan (GeneratePlan, before Approval) into
    // the master body so it EXECUTES that plan instead of re-planning from scratch.
    // Empty when no plan was generated (non-coding presets) — the skill omits the
    // section then.
    internal static string BuildPlanSection(Domain.Entities.Plan? plan)
    {
        if (plan is null || plan.Steps.Count == 0) return string.Empty;
        var steps = string.Join("\n", plan.Steps
            .OrderBy(s => s.Order)
            .Select(s => $"  [{s.Order}] {s.ChangeType}: {s.Description}"));
        return $"## Approved plan — execute this\n\n{plan.Summary}\n\n{steps}\n";
    }

    // p0179h: list of repo (sandbox) names the master can address in this run.
    // Empty for 0-1 sandboxes (no prefix needed in the prompt body), bullet
    // list under a "Repositories in this run" heading for 2+. Coupled with
    // FilesystemToolHost.Route accepting the prefix in single-sandbox mode so
    // the master can use one consistent path convention.
    private static string BuildRepoNamesSection(IEnumerable<string> sandboxKeys)
    {
        var names = sandboxKeys.Where(k => !string.IsNullOrEmpty(k)).ToList();
        if (names.Count <= 1) return string.Empty;
        var bullets = string.Join("\n", names.Select(n => $"- `{n}`"));
        return $"## Repositories in this run\n{bullets}\n";
    }

    // p0317: gathers what FetchTicket published — the conversation section, the
    // attachments section (documents materialized into the run-record dir first),
    // and the image content parts (vision-capable models only). Scan masters skip
    // document materialization (read-only surface); their conversation section is
    // rendered by ScanMasterPromptFactory instead.
    private async Task<(string Conversation, string Attachments, IReadOnlyList<AIContent> ImageParts)>
        ComposeTicketExtrasAsync(
            AgenticMasterContext context, ISandbox sandbox, string runRecordDir,
            string repoPrefix, bool isScanMaster, CancellationToken cancellationToken)
    {
        var comments = FromPipeline<TicketComment>(context.Pipeline, ContextKeys.TicketComments);
        var images = FromPipeline<TicketImageAttachment>(context.Pipeline, ContextKeys.Attachments);
        var documents = FromPipeline<TicketDocumentAttachment>(context.Pipeline, ContextKeys.TicketDocuments);
        var refs = FromPipeline<AttachmentRef>(context.Pipeline, ContextKeys.TicketAttachmentRefs);

        var materialized = isScanMaster || documents.Count == 0
            ? []
            : await documentMaterializer.MaterializeAsync(
                sandbox, runRecordDir, documents, cancellationToken);
        if (repoPrefix.Length > 0)
            materialized = materialized.Select(m => m with { Path = repoPrefix + m.Path }).ToList();

        var imageParts = context.AgentConfig.SupportsVision
            ? TicketImagePromptParts.Build(images)
            : [];

        return (
            isScanMaster ? string.Empty : TicketConversationPromptSection.Render(comments),
            TicketAttachmentPromptSection.Render(
                images.Count, imageParts.Count > 0, materialized, OtherBinaries(refs, materialized)),
            imageParts);
    }

    // Everything that is neither a viewable image nor a materialized document is
    // listed by name + size only — never downloaded, never inlined.
    private static List<AttachmentRef> OtherBinaries(
        IReadOnlyList<AttachmentRef> refs, IReadOnlyList<MaterializedTicketDocument> materialized)
    {
        var origins = materialized
            .Select(m => m.OriginFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return refs
            .Where(r => !TicketImageAttachment.IsSupportedImage(r) && !origins.Contains(r.FileName))
            .ToList();
    }

    private static IReadOnlyList<T> FromPipeline<T>(PipelineContext pipeline, string key) =>
        pipeline.TryGet<IReadOnlyList<T>>(key, out var value) && value is not null ? value : [];

    private static string BuildUserPrompt(
        Ticket? ticket, Repository repo, IEnumerable<string> sandboxKeys,
        string conversationSection, string attachmentsSection)
    {
        var ticketBlock = ticket is null
            ? "(No ticket attached — investigate the repository and proceed per pipeline goal.)"
            // p0316: ticket fields are untrusted — delimit them so an embedded injection
            // ("ignore previous instructions") reads as data, not a command to the master.
            : TicketPromptDelimiters.Wrap($"""
                **ID:** {ticket.Id}
                **Title:** {ticket.Title}
                **Description:** {ticket.Description}
                **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}
                """);

        // p0317: conversation + attachments follow the ticket block — all of it is
        // the requirement record; comment text sits inside the same delimiters.
        var header = string.Join("\n\n",
            new[] { ticketBlock, conversationSection, attachmentsSection }
                .Where(s => !string.IsNullOrEmpty(s)));

        var keys = string.Join(", ", sandboxKeys);
        return $"""
            {header}

            ## Working Repository
            **Path:** {repo.LocalPath}
            **Branch:** {repo.CurrentBranch}
            **Sandbox keys:** {keys}

            Investigate the repository, plan your change, implement it, and verify
            it (build + tests). Use the available tools — read_file, grep_in_tree,
            edit, write_file, run_command, log_decision, ask_human. When you are
            done, stop calling tools and summarise what changed.
            """;
    }
}
