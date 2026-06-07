using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Central entry point: takes a PipelineRequest, loads config,
/// resolves the pipeline, and executes it end-to-end.
/// Also supports legacy string-based input for backward compatibility.
/// </summary>
public sealed class ExecutePipelineUseCase(
    IConfigurationLoader configLoader,
    IIntentParser intentParser,
    IPipelineExecutor pipelineExecutor,
    ISourceConfigOverrider sourceConfigOverrider,
    ISkillsCatalogResolver catalogResolver,
    ISkillsCatalogPath catalogPath,
    ISkillLoader skillLoader,
    IPipelineConfigResolver pipelineConfigResolver,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    IModelPricingResolver modelPricingResolver,
    IRunCancellationRegistry cancellationRegistry,
    IActiveRunLease activeRunLease,
    ILogger<ExecutePipelineUseCase> logger)
{
    // p0242: the single-run lease is CLAIMED by the poller at enqueue; this use
    // case owns the rest of its lifecycle — ATTACH the run id on start, RENEW the
    // heartbeat while the run executes, and RELEASE on every terminal exit. Renew
    // well under the reaper's 3-min stale threshold so a legit multi-minute run
    // never looks crashed. CLI binds a no-op lease; non-ticket runs hold none.
    private static readonly TimeSpan LeaseHeartbeatInterval = TimeSpan.FromSeconds(45);
    /// <summary>
    /// Sub-path inside the catalog root where the shared concept vocabulary lives.
    /// The vocabulary is global per catalog (not per-pipeline-skills-path), so it
    /// always sits at the top of the <c>skills/</c> tree regardless of which
    /// pipeline runs.
    /// </summary>
    private const string CatalogSkillsRootSubPath = "skills";

    public async Task<CommandResult> ExecuteAsync(
        PipelineRequest request, string configPath, CancellationToken cancellationToken)
    {
        var runStartedAt = DateTimeOffset.UtcNow;
        var runId = RunIdGenerator.Generate(runStartedAt);
        using var logScope = logger.BeginScope("run={RunId}", runId);
        using var runScope = runContext.BeginScope(runId);

        var ticketDesc = request.TicketId is not null ? $" ticket #{request.TicketId.Value}" : "";
        logger.LogInformation(
            "Executing pipeline '{Pipeline}' for project '{Project}'{TicketDesc} (run {RunId})",
            request.PipelineName, request.ProjectName, ticketDesc, runId);

        var config = configLoader.LoadConfig(configPath);
        var catalogResolution = await catalogResolver.EnsureResolvedAsync(config.Skills, cancellationToken);

        var projectName = request.ProjectName.ToLowerInvariant();
        if (!config.Projects.TryGetValue(projectName, out var projectConfig))
            throw new ConfigurationException($"Project '{projectName}' not found in configuration.");

        var repos = ResolveRepos(projectConfig, request.Context);

        var commands = PipelinePresets.TryResolve(request.PipelineName)
            ?? throw new ConfigurationException($"Pipeline '{request.PipelineName}' not found in presets.");

        var resolved = pipelineConfigResolver.Resolve(projectConfig, request.PipelineName);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, runId);
        pipeline.Set(ContextKeys.RunStartedAt, runStartedAt);
        // p0230: resolve the default run_command timeout once (per-project override
        // ?? global sandbox default) so the agentic handlers can build the tool
        // host with the project's command budget instead of a hard-coded 60s.
        pipeline.Set(ContextKeys.RunCommandTimeoutSeconds,
            config.Sandbox.ResolveRunCommandTimeout(projectConfig.Sandbox));
        // p0205: the visible LoadCatalog step reads this binding to emit the
        // per-run CatalogLoaded event. EnsureResolvedAsync above is the loader;
        // the step just records what THIS run bound to.
        pipeline.Set(ContextKeys.CatalogResolution, catalogResolution);
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, repos);
        pipeline.Set(ContextKeys.ResolvedPipeline, resolved);
        pipeline.Set(ContextKeys.Headless, request.Headless);
        pipeline.Set(ContextKeys.PipelineTypeName, PipelinePresets.GetPipelineType(request.PipelineName));
        pipeline.Set(ContextKeys.PipelineName, request.PipelineName);
        pipeline.Set(ContextKeys.ConfigDir, Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".");
        pipeline.Set("ProjectPricing", resolved.Agent.Pricing);
        pipeline.Set("PipelineCostCap", config.PipelineCostCap.ResolveFor(request.PipelineName));
        // p0176b: per-call cost emitter (EventPublishingChatClient) and the
        // tracker share the same default-pricing baseline via the resolver.
        pipeline.Set("ModelPricingResolver", modelPricingResolver);

        // p0125c-followup: vocabulary must be in PipelineContext BEFORE the first
        // handler runs. Since p0125c, PipelineNameInitializer is step 1 of every
        // preset and calls SetEnum("pipeline_name", ...) — which throws
        // KeyNotFoundException against ConceptVocabulary.Empty. LoadSkills
        // populates the vocabulary too, but it's much later in every preset
        // (typically step 14+), so the early concept-writers had no vocab.
        // Loading here, between catalog-resolve and pipeline-execute, is the
        // single deterministic choke-point where both the catalog root and the
        // freshly-built PipelineContext are in scope. LoadSkills still runs
        // later but the vocabulary slot will already be populated, so the
        // double-load is a no-op (LoadSkills sets the same vocabulary).
        var vocabulary = LoadVocabularyFromCatalog();
        pipeline.Set(ContextKeys.ConceptVocabulary, vocabulary);

        if (request.TicketId is not null)
            pipeline.Set(ContextKeys.TicketId, request.TicketId);

        if (request.IsInit)
        {
            pipeline.Set(ContextKeys.InitMode, true);
            pipeline.Set(ContextKeys.CheckoutBranch, "agentsmith/init");
        }

        if (request.Context is not null)
        {
            foreach (var (key, value) in request.Context)
                pipeline.Set(key, value);

            // Map ScanBranch to CheckoutBranch if not already set
            if (request.Context.ContainsKey(ContextKeys.ScanBranch)
                && !pipeline.Has(ContextKeys.CheckoutBranch))
            {
                pipeline.Set(ContextKeys.CheckoutBranch, request.Context[ContextKeys.ScanBranch]);
            }
        }

        // p0128b: operator answers from a prior open-questions round-trip flow into
        // the next Plan-skill run as a structured input block (PromptPrefixBuilder).
        if (request.PlanAnswers is { Count: > 0 })
            pipeline.Set(ContextKeys.PlanAnswers, request.PlanAnswers);

        sourceConfigOverrider.Apply(projectConfig, pipeline);

        await PublishRunStartedAsync(runId, runStartedAt, request, repos, projectConfig, cancellationToken);
        // p0242: link the run id onto the lease the poller claimed (jobId stays
        // null for an in-process run — the heartbeat is its liveness signal). Then
        // pump the heartbeat for the run's lifetime, and RELEASE on every exit.
        await AttachLeaseAsync(request, runId, cancellationToken);
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatPump = RunHeartbeatPumpAsync(request, runId, heartbeatCts.Token);
        // p0200: register a per-run CTS so /api/runs/{runId}/cancel and
        // PipelineRunWatchdog can signal this execution by runId.
        var runCt = cancellationRegistry.Register(runId, cancellationToken);
        try
        {
        CommandResult result;
        try
        {
            result = await pipelineExecutor.ExecuteAsync(
                commands, projectConfig, pipeline, runCt);
        }
        catch (OperationCanceledException) when (runCt.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // p0200: operator-initiated cancel or the PipelineRunWatchdog
            // (per-run/per-job wall-time budget). p0232: surface WHICH — the
            // registry records the reason ("operator" / "watchdog-wall-time"),
            // but the run summary used to say a bare "cancelled" that left the
            // operator guessing. Map it to a sentence that names the budget knob.
            cancellationRegistry.TryGetReason(runId, out var cancelReason);
            var cancelMsg = cancelReason switch
            {
                "watchdog-wall-time" =>
                    $"Run exceeded its {config.Orchestrator.MaxRunWallTimeSeconds / 60}-minute wall-time budget "
                    + "(orchestrator.max_run_wall_time_seconds) and was cancelled.",
                "operator" => "Cancelled by operator.",
                // p0237: the liveness watcher killed the run because the sandbox
                // container exited mid-step (SandboxLivenessWatcher.CancelReason).
                // The usual cause is the container being OOM-killed during a heavy
                // build/test — name it so the operator checks memory, not an LLM
                // timeout (which is what the in-flight LLM call's cancellation
                // looks like, but isn't the cause).
                "sandbox-vanished" =>
                    "The sandbox container exited mid-run (it vanished) — most often an "
                    + "out-of-memory kill during a build/test. Check the sandbox container's "
                    + "memory limit and whether the build needs a `restore` step first. The "
                    + "'A task was cancelled' on the LLM call is a side effect, not the cause.",
                _ => string.IsNullOrWhiteSpace(cancelReason) ? "Cancelled." : $"Cancelled: {cancelReason}.",
            };
            var cancelCost = PipelineCostTracker.GetOrCreate(pipeline).EstimateCostUsd();
            await PublishRunFinishedAsync(runId, CommandResult.Fail(cancelMsg), cancelCost, CancellationToken.None);
            cancellationRegistry.Unregister(runId);
            return CommandResult.Fail(cancelMsg);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // p0232: an INTERNAL cancellation — not the operator/watchdog (that's
            // the runCt catch above) and not the caller's token. It means a
            // timeout fired inside a step (a sandbox command like dotnet/npm
            // restore+build, or an LLM call) and threw up the stack. The bare
            // "A task was canceled." the framework would otherwise record is
            // useless — name the active step and the levers so the operator can
            // actually act instead of guessing.
            var step = pipeline.TryGet<string>(ContextKeys.ActivePhaseStep, out var s)
                && !string.IsNullOrWhiteSpace(s) ? s : "a step";
            var reason =
                $"Cancelled during '{step}' by an internal timeout — a sandbox command "
                + "(e.g. dotnet/npm restore or build) or an LLM call exceeded its budget. "
                + "Raise sandbox.run_command_timeout_seconds / sandbox.step_timeout_seconds "
                + "(global or per-project), or limits.max_seconds_per_skill_call.";
            var timeoutCost = PipelineCostTracker.GetOrCreate(pipeline).EstimateCostUsd();
            await PublishRunFinishedAsync(runId, CommandResult.Fail(reason), timeoutCost, CancellationToken.None);
            cancellationRegistry.Unregister(runId);
            return CommandResult.Fail(reason);
        }
        catch (Exception ex)
        {
            // p0175-fix: an unhandled exception in the executor (e.g. Docker
            // sandbox image not built, Redis down, config loader throw) used
            // to escape this method without publishing RunFinished. The
            // dashboard then kept the run in its active map as status=running
            // forever — operator sees a ghost. Now we publish a terminal
            // RunFinished(failed) with the exception message as summary, then
            // rethrow so PipelineQueueConsumer's existing log path is preserved.
            // CancellationToken.None on the publish: even if the operator's
            // ct is already cancelled, the terminal event still needs to land.
            var failureCost = PipelineCostTracker.GetOrCreate(pipeline).EstimateCostUsd();
            await PublishRunFinishedAsync(runId, CommandResult.Fail(ex.Message), failureCost, CancellationToken.None);
            cancellationRegistry.Unregister(runId);
            throw;
        }
        cancellationRegistry.Unregister(runId);

        if (result.IsSuccess && pipeline.TryGet<string>(ContextKeys.PullRequestUrl, out var prUrl))
            result = result with { PrUrl = prUrl };

        var costUsd = PipelineCostTracker.GetOrCreate(pipeline).EstimateCostUsd();
        await PublishRunFinishedAsync(runId, result, costUsd, cancellationToken);
        LogResult(result, projectName, pipeline);
        return result;
        }
        finally
        {
            // p0242: the run is terminal on EVERY path out of the block above
            // (success, operator/internal cancel, throw). Stop the heartbeat pump
            // and release the lease so the ticket is reclaimable — CancellationToken
            // .None so the release lands even when the caller's token is cancelled.
            heartbeatCts.Cancel();
            try { await heartbeatPump; } catch (OperationCanceledException) { /* expected */ }
            await ReleaseLeaseAsync(request, CancellationToken.None);
        }
    }

    // p0242: lease lifecycle helpers. The poller CLAIMED the lease at enqueue; we
    // attach the run id, renew while alive, and release at the end. Guarded to
    // ticket runs — CLI/non-ticket runs hold no lease, and an ExecuteUpdate/Delete
    // against a missing row is a harmless no-op anyway.
    private async Task AttachLeaseAsync(PipelineRequest request, string runId, CancellationToken ct)
    {
        if (request.TicketId is null) return;
        await activeRunLease.AttachRunAsync(request.ProjectName, request.TicketId, runId, jobId: null, ct);
    }

    private async Task ReleaseLeaseAsync(PipelineRequest request, CancellationToken ct)
    {
        if (request.TicketId is null) return;
        await activeRunLease.ReleaseAsync(request.ProjectName, request.TicketId, ct);
    }

    private async Task RunHeartbeatPumpAsync(PipelineRequest request, string runId, CancellationToken ct)
    {
        if (request.TicketId is null) return;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(LeaseHeartbeatInterval, ct);
                await activeRunLease.RenewHeartbeatAsync(request.ProjectName, request.TicketId, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) { /* run ended — stop renewing */ }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lease heartbeat renewal failed for run {RunId}", runId);
        }
    }

    /// <summary>
    /// Resolves the repos this run will operate on. By default returns all configured repos.
    /// If ContextKeys.SourceOverrideRepo is set in the request context (CLI `--repo NAME`),
    /// filters down to that single repo; unknown names throw with the known-repos list.
    /// </summary>
    private static IReadOnlyList<RepoConnection> ResolveRepos(
        ResolvedProject project, IReadOnlyDictionary<string, object>? requestContext)
    {
        if (project.Repos.Count == 0)
            throw new InvalidOperationException(
                $"Project '{project.Name}' has no repos configured.");

        var hasScopeOverride = requestContext is not null
            && requestContext.TryGetValue(ContextKeys.SourceOverrideRepo, out var value)
            && value is string repoName
            && !string.IsNullOrEmpty(repoName);

        GuardSourceOverridesRequireRepoOnMultiRepo(project, requestContext, hasScopeOverride);

        if (!hasScopeOverride) return project.Repos;

        var target = ((string)requestContext![ContextKeys.SourceOverrideRepo]!);
        var match = project.Repos.SingleOrDefault(r =>
            string.Equals(r.Name, target, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new InvalidOperationException(
                $"--repo '{target}' does not match any repo in project '{project.Name}'. "
                + $"Known repos: [{string.Join(", ", project.Repos.Select(r => r.Name))}].");
        return new[] { match };
    }

    /// <summary>
    /// Multi-repo guard: any `--source-*` flag set without `--repo NAME` on a
    /// project with more than one configured repo is ambiguous (which repo
    /// gets overridden?). Reject with a clear error pointing at the project
    /// and the known repo list. Single-repo projects accept the flags without
    /// `--repo` (legacy ergonomics).
    /// </summary>
    private static void GuardSourceOverridesRequireRepoOnMultiRepo(
        ResolvedProject project, IReadOnlyDictionary<string, object>? requestContext, bool hasScopeOverride)
    {
        if (hasScopeOverride || project.Repos.Count <= 1 || requestContext is null) return;
        var sourceFlags = new[]
        {
            ContextKeys.SourceType, ContextKeys.SourcePath,
            ContextKeys.SourceUrl, ContextKeys.SourceAuth,
        };
        if (!sourceFlags.Any(requestContext.ContainsKey)) return;
        throw new InvalidOperationException(
            $"Project '{project.Name}' has {project.Repos.Count} repos — `--source-*` requires `--repo NAME` "
            + $"to disambiguate. Known repos: [{string.Join(", ", project.Repos.Select(r => r.Name))}].");
    }

    /// <summary>
    /// Legacy entry point for backward compatibility with string-based input.
    /// Parses intent from free text, builds a PipelineRequest, and delegates.
    /// </summary>
    public async Task<CommandResult> ExecuteAsync(
        string userInput,
        string configPath,
        bool headless,
        string? pipelineOverride,
        CancellationToken cancellationToken,
        Dictionary<string, object>? initialContext = null)
    {
        logger.LogInformation("Processing input: {Input}", userInput);

        var request = await BuildRequestFromLegacyInput(
            userInput, configPath, headless, pipelineOverride, initialContext, cancellationToken);

        return await ExecuteAsync(request, configPath, cancellationToken);
    }

    private async Task<PipelineRequest> BuildRequestFromLegacyInput(
        string userInput, string configPath, bool headless,
        string? pipelineOverride, Dictionary<string, object>? initialContext,
        CancellationToken cancellationToken)
    {
        var intent = await intentParser.ParseAsync(userInput, cancellationToken);
        var config = configLoader.LoadConfig(configPath);
        var projectName = intent.ProjectName.Value;
        var pipelineName = ResolvePipelineName(pipelineOverride, config, projectName, fallback: "fix-bug");

        return new PipelineRequest(
            projectName, pipelineName,
            TicketId: intent.TicketId,
            Headless: headless, Context: initialContext);
    }

    private string ResolvePipelineName(
        string? pipelineOverride, AgentSmithConfig config, string projectName, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(pipelineOverride)) return pipelineOverride;
        if (!config.Projects.TryGetValue(projectName, out var project)) return fallback;
        try { return pipelineConfigResolver.ResolveDefaultPipelineName(project); }
        catch (InvalidOperationException) { return fallback; }
    }

    /// <summary>
    /// p0125c-followup: shared concept vocabulary is loaded from the catalog's
    /// <c>skills/</c> subtree. <see cref="ISkillsCatalogPath.Root"/> points at
    /// the extracted catalog root (e.g. <c>~/.cache/agentsmith/skills/</c>); the
    /// vocab YAML sits at <c>{Root}/skills/concept-vocabulary.yaml</c>.
    /// Returns <see cref="ConceptVocabulary.Empty"/> when the catalog isn't
    /// bootstrapped yet (CLI tooling running before the resolver wired it up,
    /// or dev-from-source with no catalog at all). The empty vocab matches the
    /// pre-fix behavior; concept-writers in early steps will throw with the
    /// same KeyNotFoundException as before, surfacing the missing-catalog
    /// problem clearly rather than masking it.
    /// </summary>
    private ConceptVocabulary LoadVocabularyFromCatalog()
    {
        try
        {
            var skillsRoot = Path.Combine(catalogPath.Root, CatalogSkillsRootSubPath);
            if (!Directory.Exists(skillsRoot))
            {
                logger.LogWarning(
                    "Skills root {Path} not present at pipeline-bootstrap; concept vocabulary " +
                    "will be empty until LoadSkills repopulates it.", skillsRoot);
                return ConceptVocabulary.Empty;
            }
            return skillLoader.LoadVocabulary(skillsRoot) ?? ConceptVocabulary.Empty;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Catalog not yet bootstrapped at pipeline-start; concept vocabulary " +
                "will be empty until LoadSkills repopulates it.");
            return ConceptVocabulary.Empty;
        }
    }

    private Task PublishRunStartedAsync(
        string runId, DateTimeOffset runStartedAt, PipelineRequest request,
        IReadOnlyList<RepoConnection> repos, ResolvedProject projectConfig,
        CancellationToken ct)
    {
        var trigger = request.TicketId is not null ? "ticket" : "manual";
        var repoNames = repos.Select(r => r.Name).ToArray();
        // p0186: agent display label = "{type}/{model}" so the dashboard
        // can show "claude/claude-sonnet-4-20250514" or "azure_openai/gpt-4.1"
        // at-a-glance. The operator's config-key (e.g. "claude-default") is
        // not threaded through ResolvedProject today; type + model is enough
        // to answer "which agent is doing this work".
        var agent = projectConfig.Agent;
        var agentName = string.IsNullOrEmpty(agent.Model)
            ? agent.Type
            : $"{agent.Type}/{agent.Model}";
        return eventPublisher.PublishAsync(
            new RunStartedEvent(
                runId, trigger, request.PipelineName, repoNames, runStartedAt,
                agentName, request.TicketId?.Value),
            ct);
    }

    // p0176b: pipeline-aggregate cost rides on RunFinished so RunSnapshot.Apply
    // can override the per-call accumulation with the tracker's truth.
    private Task PublishRunFinishedAsync(
        string runId, CommandResult result, decimal? costUsd, CancellationToken ct) =>
        eventPublisher.PublishAsync(
            new RunFinishedEvent(
                runId,
                result.IsSuccess ? "success" : "failed",
                result.PrUrl,
                result.Message ?? string.Empty,
                DateTimeOffset.UtcNow,
                costUsd),
            ct);

    private void LogResult(CommandResult result, string projectName, PipelineContext pipeline)
    {
        var cost = PipelineCostTracker.GetOrCreate(pipeline);
        if (result.IsSuccess)
            logger.LogInformation(
                "Project {Project} processed successfully: {Message} | {Cost}",
                projectName, result.Message, cost);
        else
            logger.LogWarning(
                "Project {Project} processing failed: {Message} | {Cost}",
                projectName, result.Message, cost);
    }
}
