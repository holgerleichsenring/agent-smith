using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
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
        // p0341: seed the durable progress ledger 1:1 from the ratified plan (stable
        // framework ids + per-step target) so the master opens on the checklist. Also
        // published to PipelineContext (source of truth) for the re-drive nudges + the
        // done-status diagnostic. No plan (fix-bug self-planning) => empty seed.
        var progress = new ProgressLedgerToolHost(ProgressLedgerSeeder.Seed(plan));
        context.Pipeline.Set(ContextKeys.ProgressLedger, progress.GetLedger());
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
        var writeContextYaml = new WriteContextYamlToolHost(sandboxes, defaultKey, contextYamlSerializer);

        // p0278: a scan/review master (output_schema == observation) gets the scanner
        // findings + spec inline and a READ-ONLY surface, so it reviews instead of
        // running the coding "implement + verify build/tests" contract. Keyed on the
        // master's declared schema, NOT pipeline name; the coding path is untouched.
        var isScanMaster = string.Equals(
            schemaResolver.Resolve(context.MasterSkillName), "observation", StringComparison.OrdinalIgnoreCase);

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

        var request = new AgenticLoopRequest(
            AgentConfig: context.AgentConfig,
            TaskType: TaskType.Primary,
            SystemPrompt: masterBody,
            UserPrompt: userPrompt,
            Tools: ComposeMasterTools(isScanMaster, isSpecDialog, fs, log, human, credentials, writeContextYaml, progress, context),
            UserImageParts: extras.ImageParts);

        AgenticLoopResult loopResult;
        try
        {
            loopResult = await loopRunner.RunAsync(request, cancellationToken);
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

        var costTracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        costTracker.Track(loopResult.Response);

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
                costTracker.Track(deeper.Response);
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
                costTracker.Track(applyResult.Response);
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
                costTracker.Track(verdictResult.Response);
                verification = MasterVerificationParser.TryParse(verdictResult.Response.Text);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Verdict re-prompt failed for master '{Skill}'", context.MasterSkillName);
            }
        }

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

        logger.LogInformation(
            "Master skill '{Skill}' completed: {Count} files changed, {Decisions} decisions",
            context.MasterSkillName, changes.Count, decisions.Count);

        return CommandResult.Ok($"Master '{context.MasterSkillName}' completed: {changes.Count} files changed");
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
        ProgressLedgerToolHost progress, AgenticMasterContext context)
    {
        if (isSpecDialog) return AgenticToolSurface.SpecDialog(fs, human);
        IList<AITool> BaseSurface() => isScanMaster
            ? AgenticToolSurface.Review(fs, log)
            : AgenticToolSurface.ReadWriteWithHuman(
                fs, log, human, credentials: credentials, writeContextYaml: writeContextYaml);

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
            ChildTools: BaseSurface().ToList(), AnswerStore: childAnswerStore, Budget: subAgentBudget);
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
