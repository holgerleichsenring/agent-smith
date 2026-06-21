using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
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
        var human = new HumanToolHost(dialogueTransport);
        var credentials = new GetArtifactCredentialsToolHost(config.Registries);
        var writeContextYaml = new WriteContextYamlToolHost(sandboxes, defaultKey, contextYamlSerializer);

        // p0278: a scan/review master (output_schema == observation) gets the scanner
        // findings + spec inline and a READ-ONLY surface, so it reviews instead of
        // running the coding "implement + verify build/tests" contract. Keyed on the
        // master's declared schema, NOT pipeline name; the coding path is untouched.
        var isScanMaster = string.Equals(
            schemaResolver.Resolve(context.MasterSkillName), "observation", StringComparison.OrdinalIgnoreCase);

        var userPrompt = isScanMaster
            ? scanPromptFactory.Build(context.Pipeline, context.Repository, addressNames)
            : BuildUserPrompt(ticket, context.Repository, addressNames);

        var request = new AgenticLoopRequest(
            AgentConfig: context.AgentConfig,
            TaskType: TaskType.Primary,
            SystemPrompt: masterBody,
            UserPrompt: userPrompt,
            Tools: isScanMaster
                ? AgenticToolSurface.Review(fs, log)
                : AgenticToolSurface.ReadWriteWithHuman(
                    fs, log, human, credentials: credentials, writeContextYaml: writeContextYaml));

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
            var partialDecisions = log.GetDecisions();
            if (partialDecisions.Count > 0) context.Pipeline.AppendDecisions(partialDecisions);
            var reason = DescribeMasterFailure(ex);
            logger.LogWarning(ex, "Master skill '{Skill}' failed: {Reason}", context.MasterSkillName, reason);
            return CommandResult.Fail(reason);
        }

        var costTracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        costTracker.Track(loopResult.Response);

        var changes = fs.GetChanges();

        // p0255: the master sometimes writes a plan/decisions but applies NO source
        // edits — the recurring "investigated, planned, then stopped" run that ships
        // nothing (a correct plan.md, zero source writes). When code is expected and
        // only run-record artifacts were written, re-prompt the master ONCE with a
        // focused "apply your plan now" instruction: a bounded second shot that
        // turns a wasted no-edit run into real work. The git-authoritative keystone
        // (CommitAndPR) still gates the final outcome either way.
        var pipelineName = context.Pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null;
        if (ShouldDriveApply(pipelineName, changes))
        {
            logger.LogWarning(
                "Master '{Skill}' wrote a plan but edited no source — re-prompting once to apply it",
                context.MasterSkillName);
            try
            {
                var applyResult = await loopRunner.RunAsync(
                    request with { UserPrompt = BuildApplyNudge(userPrompt) }, cancellationToken);
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
        context.Pipeline.Set(ContextKeys.RunDurationSeconds, (int)loopResult.Duration.TotalSeconds);

        // p0267: publish the master's final answer + skill name so a downstream
        // findings-scrape (CollectMasterFindings on the api-security path) can route
        // the master's TRIAGED observation-array into SkillObservations. Unconditional
        // and content-agnostic — the coding path simply never runs a consumer.
        context.Pipeline.Set(ContextKeys.MasterAnswer, loopResult.Response.Text ?? string.Empty);
        context.Pipeline.Set(ContextKeys.MasterSkillName, context.MasterSkillName);

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
                    request with { UserPrompt = BuildVerdictNudge(userPrompt) }, cancellationToken);
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
        else if (!isScanMaster)
        {
            // p0278: a scan/review master never emits a build/test verdict — only a
            // coding master is expected to, so don't warn about its absence on a scan.
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
    private static string BuildVerdictNudge(string originalUserPrompt) =>
        "Your previous pass changed source but did NOT emit the required Phase 4 verdict, "
        + "so the run cannot be reported. Do NOT make further code changes now. Build the "
        + "project and run the automated tests the way the repository defines them, then emit "
        + "ONLY your final fenced ```verdict block reflecting the real build/test outcome "
        + "(status: green | no-tests | failed). Nothing before or after the block.\n\n"
        + "Original task:\n" + originalUserPrompt;

    // p0255: the focused second-shot prompt when the master planned but edited
    // nothing — the plan is not the deliverable, the edited source is.
    private static string BuildApplyNudge(string originalUserPrompt) =>
        "You wrote a plan but have NOT edited any source file yet. The plan is not the "
        + "deliverable — the edited source is. Apply your plan NOW: make the edits with "
        + "edit / multi_edit / write_file (repo-prefixed paths), then build, run the tests, "
        + "and emit your verdict. Do not stop until at least one SOURCE file is changed, or "
        + "you report a concrete blocker explaining why no edit was possible.\n\n"
        + "Original task:\n" + originalUserPrompt;

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

    private static string BuildUserPrompt(Ticket? ticket, Repository repo, IEnumerable<string> sandboxKeys)
    {
        var ticketBlock = ticket is null
            ? "(No ticket attached — investigate the repository and proceed per pipeline goal.)"
            : $"""
                ## Ticket
                **ID:** {ticket.Id}
                **Title:** {ticket.Title}
                **Description:** {ticket.Description}
                **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}
                """;

        var keys = string.Join(", ", sandboxKeys);
        return $"""
            {ticketBlock}

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
