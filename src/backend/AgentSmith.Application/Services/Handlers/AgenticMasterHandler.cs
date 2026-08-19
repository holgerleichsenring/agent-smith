using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Sandbox;
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
/// are involved. Coding pipelines dispatch this handler instead of the
/// legacy Triage→Plan→…→AgenticExecute chain.
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
    SandboxWorkingTreeReader workingTree, // p0411: the changed paths the state block carries
    RunWorkCheckpointer checkpointer, // p0360: mid-run work durability
    ISandboxFileReaderFactory sandboxFileReaderFactory, // p0380: memory recall/remember hosts
    IDialogueTransport? dialogueTransport,
    AgenticToolSurface toolSurface,
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
        // p0315d, p0393a: the phase-spec branch. It used to key on the pipeline name
        // "phase-execution"; that name is now an ALIAS that resolves to `code`, and every
        // code run carries a derived phase spec, so the presence of the SPEC is what
        // selects this prompt. Same master (coding-agent-master), phase-specific user
        // prompt + a ticket-parking ask_human instead of the live transport.
        var isPhaseExecution = context.Pipeline.Has(ContextKeys.PhaseSpec);
        // p0244: give the master the per-run record dir so it writes plan.md /
        // decisions.md DIRECTLY into .agentsmith/runs/{runId}/ (the same dir the
        // framework writes result.md to + reads the plan back from), instead of a
        // loose .agentsmith/plan.md that gets overwritten every run.
        var runRecordDir = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var rid)
            && !string.IsNullOrEmpty(rid)
            ? RunRecordPaths.RelativeDir(rid!)
            : RunRecordPaths.AgentSmithDir;

        // p0394a: the ratified phase spec is the run's single planning artifact —
        // it is rendered into the master body as the plan of record and seeds the
        // progress ledger below. Absent on non-code surfaces (scan, spec dialog).
        var draft = context.Pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var dr) ? dr : null;
        // p0278: a scan/review master (output_schema == observation) gets the scanner
        // findings + spec inline and a READ-ONLY surface. Keyed on the master's
        // declared schema, NOT pipeline name; computed HERE (p0356) because the
        // ledger seed + toolchain probe below are coding-master-only concerns.
        var isScanMaster = string.Equals(
            schemaResolver.Resolve(context.MasterSkillName), "observation", StringComparison.OrdinalIgnoreCase);

        // p0341, p0394a: seed the durable progress ledger 1:1 from the phase spec's
        // steps (stable spec-assigned ids + per-step target) so the master opens on
        // the checklist the keystone will verify. Also published to PipelineContext
        // (source of truth) for the re-drive nudges + the done-status diagnostic.
        // No spec (scan/dialog surfaces) => empty seed.
        // p0356: a spec-less coding run of a TICKET seen before resumes on the latest
        // prior run's persisted ledger (mid-run flushes make it durable) — gated in
        // PriorRunLedgerSeeder on progressed-past-bootstrap + the age cap.
        var seedEntries = ProgressLedgerSeeder.Seed(draft);
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
        // p0374a: the master pass a ledger update arrived in. Pass 0 is the first
        // loop; ReengageWhileProductiveAsync advances it per re-engagement pass.
        // The counter lives here because the loop does; the tool host only reads it.
        var masterPass = 0;
        Func<Contracts.Progress.ProgressLedger, IReadOnlyList<Contracts.Progress.LedgerTransition>, Task>? onReplaced =
            flusher is null && checkpointInterval <= 0
                ? null
                : async (ledger, transitions) =>
                {
                    if (flusher is not null)
                    {
                        await flusher.FlushAsync(ledger);
                        // p0374a: what CHANGED goes on the trail — the snapshot above is
                        // overwritten by the next flush and cannot hold the history.
                        await flusher.RecordTransitionsAsync(transitions);
                    }
                    await checkpointer.CheckpointAsync(
                        context.Pipeline, checkpointInterval, cancellationToken);
                };
        var progress = new ProgressLedgerToolHost(
            seedEntries, onReplaced, logger, currentPass: () => masterPass);
        context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
        if (!progress.GetLedger().IsEmpty && flusher is not null)
            await flusher.FlushAsync(progress.GetLedger());
        var masterBody = prompts.Render(context.MasterSkillName, new Dictionary<string, string>
        {
            ["ProjectContextSection"] = MasterPromptSections.BuildProjectContextSection(context.ProjectContext),
            ["CodingPrinciples"] = context.CodingPrinciples,
            ["CodeMapSection"] = MasterPromptSections.BuildCodeMapSection(context.RepoCodeMaps),
            ["RepoNames"] = MasterPromptSections.BuildRepoNamesSection(addressNames),
            ["PlanSection"] = MasterPromptSections.BuildPlanSection(draft),
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
            // p0393a: the CURRENT phase's markdown companion — the ticket spans this phase
            // must honour, carried byte-identical. ADDITIONAL context, never a replacement
            // for the pinned ticket (p0357). SpecSection is the name the skill catalog uses
            // from v4.1 on; WorkSpecSection is the same content under the pre-v4.1 pin's
            // name, and it goes when the embedded pin bumps past that release.
            ["SpecSection"] = Specs.SpecPromptSection.Build(context.Pipeline),
            ["WorkSpecSection"] = Specs.SpecPromptSection.Build(context.Pipeline),
            // p0341: the seeded checklist, so the master opens on it. Masters without
            // the placeholder (older pins) simply never render it — Render is a no-op.
            ["ProgressLedgerSection"] = progress.GetLedger().IsEmpty
                ? string.Empty
                : ProgressLedgerRenderer.Render(progress.GetLedger()),
            // p0380: the experiential-memory INDEX (one line per memory) — the
            // cheap plan-time pointer layer; bodies are pulled via recall().
            // Masters without the placeholder simply never render it.
            ["MemoryIndexSection"] = MasterPromptSections.BuildMemoryIndexSection(context.Pipeline),
            // p0312c: the pull request under review. Empty on every pipeline that
            // has no PR, so binding it here is unconditional; pr-review-master is
            // the only master that carries the placeholder.
            ["PrDiffSection"] = Prompts.PrDiffPromptSection.Build(context.Pipeline),
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
        // p0380: memory recall (a read, every surface) + remember (a proposal
        // writing only run-record-class .agentsmith/memory/ paths). Backed by
        // the default sandbox's file reader — the same seam the run-record and
        // coding-principles reads use.
        var memoryStore = new Memory.MemoryStore(
            sandboxFileReaderFactory.Create(sandboxes[defaultKey]), context.Repository.LocalPath, logger);
        var recall = new MemoryRecallToolHost(memoryStore);
        var remember = new MemoryWriteToolHost(memoryStore);
        // p0315b: the dialogue job id (spec-dialog: the session id) makes ask_human
        // live — questions publish on job:{id}:out and the thread's answers come
        // back on job:{id}:in. Absent (run jobs today) → the tool reports itself
        // unconfigured exactly as before.
        var dialogueJobId = context.Pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var djid)
            && !string.IsNullOrEmpty(djid) ? djid : null;
        // p0315d/p0391: a TICKET-triggered coding run has no live dialogue transport
        // (ephemeral container) — ask_human captures the question instead; the preset's
        // MasterOpenQuestions step posts + parks it after the loop. Keyed on the preset
        // actually carrying that step (and on there being a ticket to park), so the master
        // is never handed a door the run cannot open: without this, ask_human fell through
        // to the transport host and answered "Dialogue transport not configured" on every
        // fix-bug / add-feature / fix-no-test run.
        var parksMasterQuestions = pipelineName is not null
            && PipelinePresets.ParksMasterQuestions(pipelineName) && ticket is not null;
        var ticketClarifications = parksMasterQuestions ? new TicketClarificationToolHost() : null;
        IToolHost human = ticketClarifications is not null
            ? ticketClarifications
            : new HumanToolHost(dialogueTransport, dialogueJobId);
        var credentials = new GetArtifactCredentialsToolHost(config.Registries);
        // p0341c: constrain write_context_yaml's context_name to the DISCOVERED contexts
        // per repo (from ScopeRepos' RemoteContextInventory) so the model can't author a
        // stray 'default' when discovery already resolved e.g. [api, ...].
        var discoveredContexts = MasterPromptSections.BuildDiscoveredContexts(context.Pipeline);
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
                    : MasterUserPrompt.Build(ticket, context.Repository, addressNames,
                        extras.Conversation, extras.Attachments);

        // p0341c: the shared cost tracker + the open-loop governor hooks (within-pass
        // money fence + periodic ledger-reminder injection). Built once; reused across
        // every pass — the estimator accumulates all master iterations, the fence compares
        // start-of-master spend + that estimate against the effective cap. Only the coding
        // master (read/write, not scan/spec-dialog) gets the hooks + the large ceiling.
        var costTracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        var masterHooks = isScanMaster || isSpecDialog
            ? null
            : MasterLoopHooksFactory.Build(context, costTracker, () => progress.GetLedger(), log);
        var iterationCeiling = isScanMaster || isSpecDialog
            ? (int?)null
            : context.AgentConfig.MaxMasterLoopIterations;

        var request = new AgenticLoopRequest(
            AgentConfig: context.AgentConfig,
            TaskType: TaskType.Primary,
            SystemPrompt: masterBody,
            UserPrompt: userPrompt,
            Tools: ComposeMasterTools(
                isScanMaster, isSpecDialog, fs, log, human, credentials, writeContextYaml, web,
                progress, recall, remember, context),
            UserImageParts: extras.ImageParts,
            MaxIterations: iterationCeiling,
            MasterLoopHooks: masterHooks);

        // p0341f: every drive below continues THIS conversation instead of opening a new one.
        var conversation = new MasterConversation();
        AgenticLoopResult loopResult;
        try
        {
            loopResult = await loopRunner.RunAsync(request, cancellationToken);
            conversation.Opened(request, loopResult.Response);
        }
        catch (MasterBudgetExhaustedException budgetEx)
        {
            // p0341c: the within-pass money fence tripped — stop cleanly, ship the partial
            // work + the current ledger, and record an honest cost-cap-exhausted outcome
            // (the pipeline finalizes with a record/partial PR). Never a laundered green.
            context.Pipeline.Set(ContextKeys.CodeChanges, fs.GetChanges());
            context.Pipeline.Set(ContextKeys.PhaseCommands, fs.Commands);
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
            context.Pipeline.Set(ContextKeys.PhaseCommands, fs.Commands);
            context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
            var partialDecisions = log.GetDecisions();
            if (partialDecisions.Count > 0) context.Pipeline.AppendDecisions(partialDecisions);
            var reason = MasterOutcomes.DescribeMasterFailure(ex);
            logger.LogWarning(ex, "Master skill '{Skill}' failed: {Reason}", context.MasterSkillName, reason);
            return CommandResult.Fail(reason);
        }

        // p0341e: the coding master's spend is now recorded PER ITERATION by the governor hook
        // (MasterLoopHooksFactory → RecordIterationUsage feeds the shared tracker), so tracking the
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
            context.Pipeline.Set(ContextKeys.PhaseCommands, fs.Commands);
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
        if (MasterReengagementPolicy.ShouldDriveApply(pipelineName, changes))
        {
            logger.LogWarning(
                "Master '{Skill}' wrote a plan but edited no source — re-prompting once to apply it",
                context.MasterSkillName);
            try
            {
                var applyNudge = MasterNudges.BuildApplyNudge(userPrompt, progress.GetLedger());
                var applyResult = await loopRunner.RunAsync(
                    request with { UserPrompt = applyNudge, PriorMessages = conversation.Thread() },
                    cancellationToken);
                conversation.Continued(applyNudge, applyResult.Response);
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
        if (MasterReengagementPolicy.ShouldNudgeForVerdict(pipelineName, verification))
        {
            logger.LogWarning(
                "Master '{Skill}' changed code but emitted no verdict — re-prompting once for it",
                context.MasterSkillName);
            try
            {
                var verdictNudge = MasterNudges.BuildVerdictNudge(userPrompt, progress.GetLedger());
                var verdictResult = await loopRunner.RunAsync(
                    request with { UserPrompt = verdictNudge, PriorMessages = conversation.Thread() },
                    cancellationToken);
                conversation.Continued(verdictNudge, verdictResult.Response);
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
            conversation, setPass: p => masterPass = p, cancellationToken);
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
            return MasterOutcomes.PublishOutcome(pipeline, first.Proposal, loopResult);
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
            return MasterOutcomes.FailOutcome(pipeline, loopResult, invalid.Error);
        }

        var retryResolution = outcomeResolver.Resolve(retry.Response.Text ?? string.Empty);
        if (retryResolution is OutcomeResolved second)
            return MasterOutcomes.PublishOutcome(pipeline, second.Proposal, retry);

        var stillInvalid = (OutcomeInvalid)retryResolution;
        logger.LogWarning(
            "Design-partner terminal outcome still invalid after re-prompt: {Error}", stillInvalid.Error);
        return MasterOutcomes.FailOutcome(pipeline, retry, stillInvalid.Error);
    }

    // p0280: the master surface = its base surface (read-only Review for a scan master,
    // read/write for a coding master) PLUS spawn_agents + read_sub_agent_observations when
    // sub-agents are enabled. Children SHARE this fs (so their reads/writes aggregate into
    // the master's read-set + changes) and get the same base surface — never spawn_agents.
    // p0315b: the spec-dialog surface is content-reads + ask_human only, no sub-agents —
    // a conversation turn neither writes nor delegates.
    private IList<AITool> ComposeMasterTools(
        bool isScanMaster, bool isSpecDialog, FilesystemToolHost fs, LogDecisionToolHost log, IToolHost human,
        GetArtifactCredentialsToolHost credentials, WriteContextYamlToolHost writeContextYaml,
        WebToolHost? web, ProgressLedgerToolHost progress,
        MemoryRecallToolHost recall, MemoryWriteToolHost remember, AgenticMasterContext context)
    {
        // p0380: recall (read) + remember (memory-only proposal) join EVERY
        // master surface, including the read-only Review/scan surface.
        if (isSpecDialog) return toolSurface.SpecDialog(fs, human, web, recall, remember);
        IList<AITool> BaseSurface() => isScanMaster
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
            MasterVerification? verification, MasterConversation conversation, Action<int> setPass,
            CancellationToken cancellationToken)
    {
        var ratifiedCriteria = MasterReengagementPolicy.RatifiedCriteria(context.Pipeline);
        // p0406: the phase's own declaration of what it delivers. A knowledge phase
        // (ships_code: false) reaches the acceptance gate on its dispositions alone.
        for (var pass = 0; pass < ReengageHardSafetyCap; pass++)
        {
            // p0374a: pass 0 is the first loop, so a re-engagement pass is 1-based —
            // every ledger transition recorded from here carries the pass it happened in.
            setPass(pass + 1);
            if (!MasterReengagementPolicy.ShouldReengage(
                    pipelineName, progress.GetLedger(), verification,
                    costTracker.IsBudgetExhausted, ratifiedCriteria, changes, pass + 1))
            {
                LogVerdictlessStop(context.MasterSkillName, verification, pass + 1, ratifiedCriteria.Count);
                break;
            }
            if (ticketClarifications?.Captured is not null)
                break; // an operator question short-circuits — the caller parks the run

            var passThrew = false;
            MasterBlockedClaim? blockedClaim = null;
            var toolCallsInPass = 0;
            // p0391: the per-pass ledger snapshot the turnover bound compares against — the
            // ids the checklist carried INTO this pass. A pass that ends with only ids it
            // invented itself, having written nothing, refilled its own work.
            var ledgerIdsAtPassStart = progress.GetLedger().Entries
                .Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
            var changesAtPassStart = changes;

            logger.LogInformation(
                "Master '{Skill}' re-engaging the open loop — {Remaining} actionable step(s) remain, budget OK",
                context.MasterSkillName, progress.GetLedger().ActionablePending.Count);
            try
            {
                // p0411: read the working tree HERE, once per pass, so the nudge opens with
                // the changed paths instead of leaving them to be asked for.
                var changedPaths = await ReadChangedPathsAsync(context, cancellationToken);
                var nudge = MasterNudges.BuildReengageNudge(
                    userPrompt, progress.GetLedger(), log.GetDecisions(), verification,
                    changedPaths, StagedRegistries(context.Pipeline));
                var reengaged = await loopRunner.RunAsync(
                    request with { UserPrompt = nudge, PriorMessages = conversation.Thread() },
                    cancellationToken);
                conversation.Continued(nudge, reengaged.Response);
                // p0341e: no-op for the coding master (per-iteration governor hook already
                // recorded this pass's spend); the shared helper keeps the gating in one place.
                trackMasterResponse(reengaged.Response);
                loopResult = reengaged;
                changes = fs.GetChanges();
                var reparsed = MasterVerificationParser.TryParse(reengaged.Response.Text);
                if (reparsed is not null) verification = reparsed;
                blockedClaim = MasterVerificationParser.TryParseBlockedClaim(reengaged.Response.Text);
                toolCallsInPass = ReengageProgressPolicy.CountToolCalls(reengaged.Response);
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

            // p0365: the model controls the pass boundary and naturally alternates edit passes
            // and verify passes, so per-pass state deltas mis-judge progress. The one reliable
            // idle signal is a pass that called NO tool — the model, re-engaged, did nothing.
            // Keep driving while any tool fires; budget / wall-time / the hard cap bound the
            // active-but-unconverging case, and repetition is surfaced to the operator, not auto-killed.
            // p0391: …and the ledger-turnover bound on top — the model-owned checklist has no
            // turnover limit, so a pass that only re-invents work is stopped mechanically.
            var selfRefilled = ReengageProgressPolicy.IsSelfRefilled(
                ledgerIdsAtPassStart, progress.GetLedger(),
                ReengageProgressPolicy.ProducedNewWork(changesAtPassStart, changes));
            var outcome = ReengageProgressPolicy.Decide(
                toolCallsInPass, blockedClaim, passThrew, selfRefilled);
            if (outcome != ReengageOutcome.Continue)
            {
                LogReengageStop(context.MasterSkillName, outcome, blockedClaim);
                break;
            }
        }

        return (loopResult, changes, verification);
    }

    // p0406: the open loop stopped while the contract was still unsatisfied and the master
    // had never emitted a verdict. That is a NAMED outcome — an unknown verdict the keystone
    // will record honestly — not the silent break it used to be.
    private void LogVerdictlessStop(
        string skill, MasterVerification? verification, int reengagePass, int criteriaCount)
    {
        if (!MasterAcceptanceGate.VerdictlessAfterOneRedrive(verification, reengagePass, criteriaCount)) return;
        logger.LogWarning(
            "Master '{Skill}' emitted no verification verdict across {Passes} pass(es) — ending the "
            + "open loop on an unknown verdict rather than re-driving a null", skill, reengagePass);
    }

    // p0411: the run's changed paths, repo-prefixed as the master addresses them.
    private Task<IReadOnlyList<string>> ReadChangedPathsAsync(
        AgenticMasterContext context, CancellationToken ct) =>
        workingTree.ChangedPathsAsync(
            context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes),
            context.Pipeline.TryGet<IReadOnlyDictionary<string, string>>(
                ContextKeys.SandboxRepos, out var keyToRepo) ? keyToRepo : null,
            ct);

    // p0365: an empty pass (no tool call) is surfaced as an idle stop, not a silent truncation;
    // an honest blocked claim with a concrete blocker is respected and named. Both stop the loop;
    // the keystone still records the run's honest outcome from the drained/undrained ledger.
    private void LogReengageStop(string skill, ReengageOutcome outcome, MasterBlockedClaim? block)
    {
        if (outcome == ReengageOutcome.StopBlocked)
            logger.LogInformation(
                "Master '{Skill}' reported a concrete blocker — stopping the open loop (respected): {Blocker}",
                skill, block?.Blocker);
        else if (outcome == ReengageOutcome.StopSelfRefilled)
            logger.LogInformation(
                "Master '{Skill}' re-engagement pass refilled its own checklist — every remaining step was "
                + "added during the pass and nothing reached the diff; stopping the open loop so the run "
                + "ends on its verdict instead of on the budget", skill);
        else
            logger.LogInformation(
                "Master '{Skill}' re-engagement pass called no tool — the model is idle with work still open; "
                + "stopping and surfacing for review", skill);
    }

    // p0365: the re-engagement stop is now in ReengageProgressPolicy — an empty pass (no tool
    // call) or an honest concrete blocker, never a per-pass state-delta classification.

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
    // p0422: what the framework staged for this run, so the master states it rather than
    // theorising about it — run 22 wrote "no credentials in sandbox" without ever trying.
    private static IReadOnlyList<string>? StagedRegistries(PipelineContext pipeline) =>
        pipeline.TryGet<List<string>>(ContextKeys.StagedRegistries, out var staged) ? staged : null;

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

}
