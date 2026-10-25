using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Turns;
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
using AgentSmith.Contracts.Turns;
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
    MasterRecordToolHosts recordHosts, // 2026-10-13-8762m: log_decision, recall, remember, hand_forward
    AgentSmithConfig config,
    IContextYamlSerializer contextYamlSerializer,
    ContextDocumentGate contextDocumentGate, // 2026-08-25-c9c7: judges write_context_yaml's document
    SandboxContextYamlWriter contextYamlWriter, // 2026-08-26-364f: preserves what the document omits
    Tools.VerifyDerivationStamp verifyDerivationStamp, // 2026-09-01-e14d: hashes the verify block's source
    ContextImageReview imageReview, // 2026-10-12-6bfac / 50bdb: a missing stack.image is refused, a refused one said
    IMasterOutputSchemaResolver schemaResolver,
    IScanMasterPromptFactory scanPromptFactory,
    ScanMasterPasses scanPasses, // 2026-09-01-7df4/0e80: the scan's follow-up passes
    ISpecExecutionPromptFactory specPromptFactory,
    SpecDialogTurnFlow dialogTurn, // the dialog turn's prompt and reply, by the master that leads it
    ITicketDocumentMaterializer documentMaterializer,
    MasterTemplateScopes templateScopes, // 2026-09-13-6f35: the templates this spec is built after
    WebToolHost webToolHost,
    IEventPublisher eventPublisher, // p0356: mid-run ledger flushes
    Resume.PriorRunSeedSource priorRunSeed, // p0356: same-ticket resume seed
    ISandboxToolchainProbe toolchainProbe, // p0356: probed capability line
    MasterReengagementLoop reengagement, // 2026-10-13-9a59f: the open loop, lifted out
    Passes.MasterPassRecorder passes, // 2026-10-13-9a59f: every master pass of a spec, recorded
    RunWorkCheckpointer checkpointer, // p0360: mid-run work durability
    Memory.MemoryIndexReader memoryIndex, // 2026-10-13-8762m: re-read at every spec-execution start
    IDialogueTransport? dialogueTransport,
    MasterToolComposition composition, // which tool surface this master gets
    TurnActivityTools reportingTools, // 2026-09-17-042ee: the surface those steps come from
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
        // p0315d, p0393a: the spec branch. It used to key on the pipeline name
        // "phase-execution"; that name is now an ALIAS that resolves to `code`, and every
        // code run carries a derived spec, so the presence of the SPEC is what
        // selects this prompt. Same master (coding-agent-master), spec-specific user
        // prompt + a ticket-parking ask_human instead of the live transport.
        var isSpecExecution = context.Pipeline.Has(ContextKeys.CurrentSpec);
        if (isSpecExecution) await memoryIndex.RefreshAsync(context.Pipeline, cancellationToken); // what spec N remembered
        // p0244: give the master the per-run record dir so it writes plan.md /
        // decisions.md DIRECTLY into .agentsmith/runs/{runId}/ (the same dir the
        // framework writes result.md to + reads the plan back from), instead of a
        // loose .agentsmith/plan.md that gets overwritten every run.
        var runRecordDir = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var rid)
            && !string.IsNullOrEmpty(rid)
            ? RunRecordPaths.RelativeDir(rid!)
            : RunRecordPaths.AgentSmithDir;

        // p0394a: the ratified spec is the run's single planning artifact —
        // it is rendered into the master body as the plan of record and seeds the
        // progress ledger below. Absent on non-code surfaces (scan, spec dialog).
        var draft = context.Pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var dr) ? dr : null;
        // p0278: a scan/review master (output_schema == observation) gets the scanner
        // findings + spec inline and a READ-ONLY surface. Keyed on the master's
        // declared schema, NOT pipeline name; computed HERE (p0356) because the
        // ledger seed + toolchain probe below are coding-master-only concerns.
        var isScanMaster = string.Equals(
            schemaResolver.Resolve(context.MasterSkillName), "observation", StringComparison.OrdinalIgnoreCase);

        // p0341, p0394a: seed the durable progress ledger 1:1 from the spec's
        // steps (stable spec-assigned ids + per-step target) so the master opens on
        // the checklist the keystone will verify. Also published to PipelineContext
        // (source of truth) for the re-drive nudges + the done-status diagnostic.
        // No spec (scan/dialog surfaces) => empty seed.
        // p0356: a spec-less coding run of a TICKET seen before resumes on the latest
        // prior run's persisted ledger (mid-run flushes make it durable) — gated in
        // PriorRunLedgerSeeder on progressed-past-bootstrap + the age cap.
        var seedEntries = ProgressLedgerSeeder.Seed(draft);
        if (seedEntries.Count == 0 && !isScanMaster && !isSpecDialog && ticket is not null)
            seedEntries = await priorRunSeed.SeedAsync(
                context.Pipeline, ticket.Id.Value, cancellationToken);
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
        // loop; MasterReengagementLoop advances it per re-engagement pass.
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
        // 2026-09-13-6f35: the templates this spec's contexts declare, materialised now so
        // an unreachable one fails the spec before the first token, and addressable by name
        // for the rest of this method. They never enter ContextKeys.Sandboxes — see
        // MasterTemplateAttachment for why that map is the one they stay out of.
        await using var templates = await templateScopes.OpenAsync(
            context.Pipeline, draft, isScanMaster || isSpecDialog, cancellationToken);
        // The tool host resolves by NAME, so a template flips a single-repo run into prefixed
        // paths — including the target's own. That is the addressable set, not the checkouts.
        IReadOnlyList<string> allAddresses = templates.Names.Count == 0
            ? addressNames
            : [.. addressNames, .. templates.Names];
        var masterBody = prompts.Render(context.MasterSkillName, new Dictionary<string, string>
        {
            ["ProjectContextSection"] = MasterPromptSections.BuildProjectContextSection(context.ProjectContext),
            ["CodingPrinciples"] = context.CodingPrinciples,
            ["CodeMapSection"] = MasterPromptSections.BuildCodeMapSection(context.RepoCodeMaps),
            ["RepoNames"] = MasterPromptSections.BuildRepoNamesSection(allAddresses),
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
            // p0393a: the CURRENT spec's markdown companion — the ticket spans this spec
            // must honour, carried byte-identical. ADDITIONAL context, never a replacement
            // for the pinned ticket (p0357). 2026-08-28-489a removed the pre-v4.1 alias
            // that bound the same content twice: no master in the supported catalog uses it.
            ["SpecSection"] = Specs.SpecPromptSection.Build(context.Pipeline),
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
        // p0495: the operator's step cap is the ceiling a run_command may ask for.
        var stepTimeoutCap = context.Pipeline.TryGet<int>(ContextKeys.StepTimeoutSeconds, out var stc)
            ? stc : (int?)null;
        // p0258: pass the logger so the master's file tool calls are visible
        // (`tool_call: WriteFile path=… bytes=…`). Without it the ToolHost was
        // constructed logger-less and we were BLIND to what the master actually
        // wrote — masking the "recorded N files changed but git diff is empty"
        // root cause (no real working-tree change vs wrong path vs no-op edit).
        // p0469: the log is the SPEC's, opened here and published here — before the loop
        // runs, so every exit path hands the account what the agent ran, and a spliced
        // repair pass records onto the first pass's list instead of replacing it.
        var fs = new FilesystemToolHost(
            sandboxes, defaultKey, context.Repository.LocalPath,
            runCommandTimeoutSeconds: runCommandTimeout, stepTimeoutCapSeconds: stepTimeoutCap,
            keyToRepo: keyToRepo, logger: logger)
        { Commands = Specs.SpecRunCommandScope.Open(context.Pipeline) };
        templates.AttachTo(fs); // 2026-09-13-6f35: reachable by name, refused on write by kind
        var records = recordHosts.Create(context.Pipeline, context.Repository.LocalPath);
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
        // 2026-09-22-9519: the withdrawal door, built on the SAME gate ask_human is built on — a
        // dialogue identity. The port is seeded by the turn runner that has one, so a run with no
        // conversation behind it never constructs it and never carries the tool.
        var withdraw = dialogueJobId is not null
            && context.Pipeline.TryGet<IFiledTicketWithdrawal>(
                ContextKeys.SpecDialogWithdrawal, out var withdrawal) && withdrawal is not null
            ? new WithdrawFiledTicketToolHost(withdrawal, dialogueJobId)
            : null;
        // 2026-09-27-481ba: the way out of a bounded seed, on the same gate the withdrawal is on
        // — a dialogue identity and a port the turn runner seeded, so a run with no conversation
        // behind it constructs neither.
        var readTicket = dialogueJobId is not null
            && context.Pipeline.TryGet<ITicketReader>(
                ContextKeys.SpecDialogTicketReader, out var ticketReader) && ticketReader is not null
            ? new ReadTicketToolHost(ticketReader)
            : null;
        // 2026-09-28-1da5c: and what the tracker shows against that ticket, on the same gate.
        var ticketWork = dialogueJobId is not null
            && context.Pipeline.TryGet<IBoundTicketWork>(
                ContextKeys.SpecDialogTicketWork, out var boundWork) && boundWork is not null
            ? new TicketWorkToolHost(boundWork)
            : null;
        var ticketRuns = dialogueJobId is not null
            && context.Pipeline.TryGet<IBoundTicketRuns>(
                ContextKeys.SpecDialogTicketRuns, out var boundRuns) && boundRuns is not null
            ? new TicketRunsToolHost(boundRuns)
            : null;
        var credentials = new GetArtifactCredentialsToolHost(config.Registries);
        // p0341c: constrain write_context_yaml's context_name to the DISCOVERED contexts
        // per repo (from ScopeRepos' RemoteContextInventory) so the model can't author a
        // stray 'default' when discovery already resolved e.g. [api, ...].
        var discoveredContexts = MasterPromptSections.BuildDiscoveredContexts(context.Pipeline);
        var writeDefaultRepoName = keyToRepo is not null
            && keyToRepo.TryGetValue(defaultKey, out var drn) && !string.IsNullOrEmpty(drn)
            ? drn : defaultKey;
        var writeContextYaml = new WriteContextYamlToolHost(
            sandboxes, defaultKey, contextYamlSerializer, contextDocumentGate,
            contextYamlWriter, verifyDerivationStamp, imageReview, discoveredContexts,
            writeDefaultRepoName);

        // p0356: the probed toolchain inventory enters the CODING master's system
        // prompt as a capability statement — per-run stable, so the automatic
        // prompt-cache anchoring is unaffected. Scan masters review read-only and
        // spec-dialog turns run no commands; neither is probed.
        if (!isScanMaster && !isSpecDialog)
        {
            var toolchainSection = await toolchainProbe.ProbeAsync(
                context.Pipeline, sandboxes, keyToRepo, cancellationToken);
            if (!string.IsNullOrEmpty(toolchainSection)) masterBody += "\n\n" + toolchainSection;
        }
        // 2026-09-13-ed5a: over the ADDRESSES, so the spec dialog — which seeds its
        // templates into the sandbox map this list is read from — gets the same section.
        masterBody += templates.Section(allAddresses) + ReferencePromptSection.Build(allAddresses, sandboxes) // + 283dc, 075dd
            + DesignSystemPromptSection.Render(context.Pipeline) // 2026-10-01-283dg
            + ReferencePromptSection.Carried(context.Pipeline, allAddresses.Count > 1); // 2026-10-01-283df

        // Every master surface gets web_fetch — a read-only GET of a public URL that
        // mutates nothing, so even the read-only scan surface carries it safely.
        var web = webToolHost;

        // p0317: the whole ticket reaches the master — conversation (delimited),
        // materialized documents + binary listing, and image content parts when
        // the model is vision-capable ("N images, not viewable" note otherwise).
        // A spec-dialog turn has no ticket, so it composes nothing; a spec-
        // execution run gets the SAME extras as the coding path — the hydrated
        // comment thread is exactly what a re-triggered run parked on a
        // clarification needs (closes the p0315d parked-while-answered residual).
        var repoPrefix = allAddresses.Count > 1 && keyToRepo is not null
            && keyToRepo.TryGetValue(defaultKey, out var defaultRepoName)
            ? $"{defaultRepoName}/"
            : string.Empty;
        var dialog = isSpecDialog ? DialogImageParts.From(context) : DialogImageParts.None;
        var extras = isSpecDialog
            ? (Conversation: string.Empty, Attachments: string.Empty, ImageParts: dialog.Parts)
            : await ComposeTicketExtrasAsync(
                context, sandboxes[defaultKey], runRecordDir, repoPrefix, isScanMaster, cancellationToken);

        var userPrompt = isSpecDialog
            ? dialogTurn.Prompt(context, dialog)
            : isSpecExecution
                ? specPromptFactory.Build(
                    context.Pipeline,
                    ticket ?? throw new InvalidOperationException(
                        "Spec-execution run has no ticket — FetchTicket must run before the master."),
                    context.Repository, addressNames,
                    extras.Conversation, extras.Attachments)
                : isScanMaster
                    ? scanPromptFactory.Build(context.Pipeline, context.Repository, addressNames)
                    : MasterUserPrompt.Build(ticket, context.Repository, addressNames,
                        extras.Conversation, extras.Attachments);
        // 2026-10-01-7f7ad: the Figma links of ticket or conversation, rebuilt from key and node.
        if (!isScanMaster)
            userPrompt += DesignReferenceSection.Render(DesignReferenceTexts.From(context.Pipeline, ticket),
                Design.PipelineDesignSources.Figma(context.Pipeline).Count > 0);

        // p0341c: the shared cost tracker + the open-loop governor hooks (within-pass
        // money fence + periodic ledger-reminder injection). Built once; reused across
        // every pass — the estimator accumulates all master iterations, the fence compares
        // start-of-master spend + that estimate against the effective cap. Only the coding
        // master (read/write, not scan/spec-dialog) gets the hooks + the large ceiling.
        var costTracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        // A complete checklist makes the master owe its verdict — the brake's handler-side state.
        var verdictOwed = new VerdictOwed(context.MasterSkillName, () => progress.GetLedger(),
            costTracker.EstimateCostUsd, context.AgentConfig.VerdictOwedAfterIterations, logger);
        var masterHooks = isScanMaster || isSpecDialog
            ? null
            : MasterLoopHooksFactory.Build(context, costTracker, () => progress.GetLedger(), records.Log, verdictOwed);
        var iterationCeiling = isScanMaster || isSpecDialog
            ? (int?)null
            : context.AgentConfig.MaxMasterLoopIterations;

        // 2026-09-17-042ee/2026-09-22-5891: ONLY the design surface reports, wrapped where it
        // is CONSUMED — it has two exits now, and a turn that spawns nothing must still report.
        var composed = composition.Compose(
            isScanMaster, isSpecDialog, fs, records, human, credentials, writeContextYaml, web,
            progress, withdraw, readTicket, ticketWork, ticketRuns, context);
        var masterTools = isSpecDialog ? reportingTools.Reporting(composed) : composed;

        var request = new AgenticLoopRequest(
            AgentConfig: context.AgentConfig,
            TaskType: TaskType.Primary,
            SystemPrompt: masterBody,
            UserPrompt: userPrompt,
            Tools: masterTools,
            UserImageParts: extras.ImageParts,
            MaxIterations: iterationCeiling,
            MasterLoopHooks: masterHooks);
        // The observation-schema surface's own ceiling, output budget and reduction.
        if (isScanMaster)
            request = ScanSurfaceLimits.Apply(request, context.AgentConfig, context.Pipeline);

        // p0341f: every drive below continues THIS conversation instead of opening a new one.
        var conversation = new MasterConversation();
        // 2026-10-13-9a59f: pass 0 — the first loop with its apply drive and verdict nudge — is a pass
        // of the spec; opened here, synchronously, so every call it makes carries it.
        var pass0 = passes.Open(context.Pipeline, passInStep: 0);
        Task EndPass0(string reason) => passes.EndAsync(context.Pipeline, pass0, reason, null,
            new Passes.MasterPassState(progress.GetLedger(), fs.GetChanges(), null, null));
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
            context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
            var partial = records.Log.GetDecisions();
            if (partial.Count > 0) context.Pipeline.AppendDecisions(partial);
            logger.LogWarning(
                "Master '{Skill}' stopped on the per-pipeline cost budget: {Reason}",
                context.MasterSkillName, budgetEx.Message);
            await EndPass0(Contracts.Passes.MasterPassReasons.Budget);
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
            var partialDecisions = records.Log.GetDecisions();
            if (partialDecisions.Count > 0) context.Pipeline.AppendDecisions(partialDecisions);
            var reason = MasterOutcomes.DescribeMasterFailure(ex);
            logger.LogWarning(ex, "Master skill '{Skill}' failed: {Reason}", context.MasterSkillName, reason);
            await EndPass0(Contracts.Passes.MasterPassReasons.Failed);
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
            var partial = records.Log.GetDecisions();
            if (partial.Count > 0) context.Pipeline.AppendDecisions(partial);
            context.Pipeline.Set<IReadOnlyList<Domain.Entities.PlanOpenQuestion>>(
                ContextKeys.MasterOpenQuestions, [masterQuestion]);
            logger.LogInformation(
                "Master '{Skill}' asked for clarification mid-run — pausing for the ticket answer",
                context.MasterSkillName);
            await EndPass0(Contracts.Passes.MasterPassReasons.Question);
            return CommandResult.Ok("awaiting_user_input: master asked for clarification mid-run");
        }

        // p0279: a scan/review master that barely read the source did a shallow pass —
        // re-prompt ONCE to inventory the full surface and review each area, reading its
        // code. Coverage signal = distinct source reads (FilesystemToolHost.ReadPaths);
        // bounded, scan-only. Prevents a near-empty pass; it does not guarantee every
        // class is checked (model concern). The same fs accumulates the deeper reads.
        // p0279 / 2026-09-01-7df4 / 2026-09-01-0e80: the deeper coverage pass and the
        // reconciliation against the scanner output. Every pass ADDS to the answer.
        string? scanAnswer = null;
        if (isScanMaster)
        {
            var passes = await scanPasses.DriveAsync(
                context.Pipeline, request, userPrompt, loopResult, conversation,
                fs.ReadPaths.Count, context.AgentConfig.ScanMinSourceReads,
                TrackMasterResponse, cancellationToken);
            loopResult = passes.Result;
            scanAnswer = passes.Answer;
        }

        // p0315b/p0315e: a dialog reply's typed terminal outcome resolves BEFORE it is shown.
        if (isSpecDialog)
            loopResult = await dialogTurn.ConcludeAsync(
                context, request, userPrompt, loopResult, conversation, costTracker, cancellationToken);

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
                loopResult = applyResult; // the verdict comes from the apply pass
                changes = fs.GetChanges();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The apply retry failed on its own — keep the first pass; the
                // keystone records the run as FAILED with a concrete reason.
                logger.LogWarning(ex, "Apply re-prompt failed for master '{Skill}'", context.MasterSkillName);
            }
        }

        var decisions = records.Log.GetDecisions();

        context.Pipeline.Set(ContextKeys.CodeChanges, changes);
        // p0341: the final ledger for the done-status diagnostic (WriteRunResult) + result.md.
        context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());

        // p0267: publish the master's final answer + skill name so a downstream
        // findings-scrape (CollectMasterFindings on the api-security path) can route
        // the master's TRIAGED observation-array into SkillObservations. Unconditional
        // and content-agnostic — the coding path simply never runs a consumer.
        context.Pipeline.Set(
            ContextKeys.MasterAnswer, scanAnswer ?? loopResult.Response.Text ?? string.Empty);
        context.Pipeline.Set(ContextKeys.MasterSkillName, context.MasterSkillName);
        // p0279: publish the scan master's read-set (post re-drive) so the findings scrape
        // can downgrade an analyzed_from_source claim on a file the master never read.
        context.Pipeline.Set(ContextKeys.MasterReadPaths, fs.ReadPaths.ToList());
        // 2026-09-01-3653: what the pass was given and how far it got, so the run's own
        // account can answer both instead of the next argument guessing again.
        context.Pipeline.Set(ContextKeys.MasterSystemPromptChars, masterBody.Length);
        context.Pipeline.Set(ContextKeys.MasterTurnsUsed, MasterTurnCount.From(loopResult.Response));

        // p0241: parse the master's structured verification verdict from its final
        // answer and publish it for the keystone. The model owns running the
        // build/tests and declaring the result; the framework only enforces that
        // an unverified/red run is never reported as success (CommitAndPRHandler).
        var verification = MasterVerificationParser.TryParse(loopResult.Response.Text);

        // p0263: the master changed source but emitted no parseable verdict ("Your verdict") — a
        // model-fitness miss (gpt-4.1-class models do the work yet skip the closing
        // artifact, sinking the run at the keystone). Sibling to the p0255 apply-drive:
        // when a verdict is EXPECTED (a green-tests pipeline) and none was parsed,
        // re-prompt the master ONCE to verify (no further edits) and emit ONLY the
        // verdict, then re-parse. Bounded; the git + verdict keystone still gates.
        if (MasterReengagementPolicy.ShouldNudgeForVerdict(pipelineName, verification, verdictOwed.Demanded))
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

        // p0341c: the OPEN loop's re-engagement — MasterReengagementLoop; it ends pass 0 with its first decision.
        pass0?.Dispose();
        (loopResult, changes, verification) = await reengagement.RunAsync(
            new ReengageSession(context, request, userPrompt, pipelineName, progress, fs, records.Log, verdictOwed,
                costTracker, TrackMasterResponse, ticketClarifications, conversation, p => masterPass = p, records.Decisions),
            new ReengageState(loopResult, changes, verification), pass0, verification, cancellationToken);
        if (ticketClarifications?.Captured is { } reengageQuestion)
        {
            context.Pipeline.Set(ContextKeys.CodeChanges, changes);
            var partialQ = records.Log.GetDecisions();
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
            DeclinedCriteriaLedger.Record(context.Pipeline, verification);
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

        return MasterStepResult.Completed(context.Pipeline, context.MasterSkillName, changes.Count); // a196a
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
        var images = MasterPipelineFacts.ListFrom<TicketImageAttachment>(context.Pipeline, ContextKeys.Attachments);
        var documents = MasterPipelineFacts.ListFrom<TicketDocumentAttachment>(context.Pipeline, ContextKeys.TicketDocuments);
        var refs = MasterPipelineFacts.ListFrom<AttachmentRef>(context.Pipeline, ContextKeys.TicketAttachmentRefs);

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
            isScanMaster ? string.Empty : TicketConversationPromptSection.Render(context.Pipeline),
            TicketAttachmentPromptSection.Render(
                images, imageParts.Count, materialized, MasterPipelineFacts.OtherBinaries(refs, materialized)),
            imageParts);
    }

}
