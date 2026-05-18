using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;


namespace AgentSmith.Application.Services;

/// <summary>
/// Executes a pipeline by building contexts from command names and dispatching
/// them through the CommandExecutor. Same-(Name, Round) skill-round commands are
/// batched and run in parallel via PipelineBatchRunner when the parallelism knob > 1;
/// otherwise the loop is sequential and behaves exactly as before.
/// Stops on first failure. Supports runtime command insertion via CommandResult.InsertNext.
/// Posts status updates and error reports to the ticket provider.
/// Cross-process lifecycle (status transitions + Redis heartbeat) is delegated to
/// IPipelineLifecycleCoordinator — Server uses a ticket-aware coordinator, CLI uses NoOp.
/// </summary>
public sealed class PipelineExecutor(
    ICommandExecutor commandExecutor,
    ICommandContextFactory contextFactory,
    ITicketProviderFactory ticketFactory,
    IPipelineLifecycleCoordinator lifecycleCoordinator,
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder sandboxSpecBuilder,
    AgentSmith.Application.Services.Sandbox.ISandboxLanguageResolver sandboxLanguageResolver,
    IProgressReporter progressReporter,
    IPhaseDataFlowResolver dataFlowResolver,
    AgentSmithConfig agentSmithConfig,
    ILogger<PipelineExecutor> logger) : IPipelineExecutor
{
    private const int MaxCommandExecutions = 100;

    // Post-p0117b every command that touches the project tree goes through the sandbox
    // (Repository.LocalPath = "/work" const; SandboxFileReader for reads/writes; scanners
    // and bootstrap services all sandbox-routed). The InProcessSandboxFactory used in CLI
    // mode just allocates a tempdir, so the cost of creating one is trivial.
    private static readonly HashSet<string> SandboxRequiringCommands = new(StringComparer.Ordinal)
    {
        // Source + lifecycle.
        // TryCheckoutSource is intentionally NOT here — its handler clones host-side
        // via IHostSourceCloner and never touches ISandbox. Listing it would force
        // upfront sandbox creation before the handler runs, breaking the
        // InitialSourcePath handoff for the InProcessSandboxFactory.
        CommandNames.CheckoutSource, CommandNames.AcquireSource,
        CommandNames.AgenticExecute, CommandNames.Test,
        CommandNames.GenerateTests, CommandNames.GenerateDocs,
        CommandNames.CommitAndPR, CommandNames.InitCommit, CommandNames.PersistWorkBranch,
        // Project metadata reads/writes
        CommandNames.BootstrapProject, CommandNames.BootstrapDocument,
        CommandNames.BootstrapCheck, // p0130a-era: handler reads /work/.agentsmith/* via ISandbox
        CommandNames.LoadContext, CommandNames.LoadCodingPrinciples, CommandNames.LoadCodeMap,
        CommandNames.LoadRuns, CommandNames.AnalyzeCode,
        CommandNames.CompileDiscussion, CommandNames.CompileKnowledge, CommandNames.QueryKnowledge,
        CommandNames.WriteRunResult,
        // Security scanners + post-processors
        CommandNames.StaticPatternScan, CommandNames.GitHistoryScan, CommandNames.DependencyAudit,
        CommandNames.SecurityTrend, CommandNames.SecuritySnapshotWrite, CommandNames.SpawnFix
    };

    public async Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        ResolvedProject projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting pipeline with {Count} commands", commandNames.Count);
        for (var i = 0; i < commandNames.Count; i++)
            logger.LogInformation("  [{Index}/{Total}] {Command}",
                i + 1, commandNames.Count, commandNames[i]);

        await PostTicketStatusAsync(projectConfig, context,
            "Agent Smith is working on this issue...", cancellationToken);

        await using var lifecycle = await lifecycleCoordinator.BeginAsync(projectConfig, context, cancellationToken);
        ISandbox? sandbox = null;
        try
        {
            var commands = new LinkedList<PipelineCommand>(
                commandNames.Select(PipelineCommand.Simple));
            var resolvedAgent = context.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var rp)
                ? rp!.Agent
                : projectConfig.Agent;
            var maxConcurrent = resolvedAgent.Parallelism.MaxConcurrentSkillRounds;
            var current = commands.First;
            var executionCount = 0;

            while (current is not null)
            {
                var batch = PeelBatch(current, maxConcurrent);

                // Lazy sandbox creation: build it only when the first sandbox-requiring
                // command in the batch comes up, so handlers that publish context the
                // sandbox depends on (e.g. TryCheckoutSourceHandler → SourcePath) have
                // already run.
                if (sandbox is null && batch.Any(b => SandboxRequiringCommands.Contains(b.Value.Name)))
                {
                    sandbox = await TryCreateSandboxAsync(projectConfig, context, cancellationToken);
                }

                if (executionCount + batch.Count > MaxCommandExecutions)
                {
                    lifecycle.MarkFailed();
                    return CommandResult.Fail(
                        $"Pipeline exceeded maximum of {MaxCommandExecutions} command executions. " +
                        "Possible infinite loop in command insertion.");
                }

                var (result, advanceTo) = batch.Count == 1
                    ? await ExecuteSingleStepAsync(
                        current, commands, projectConfig, context, ++executionCount, cancellationToken)
                    : await ExecuteBatchStepAsync(
                        batch, commands, projectConfig, context, executionCount + 1, cancellationToken);

                if (batch.Count > 1) executionCount += batch.Count;

                if (!result.IsSuccess)
                {
                    await TryPersistWorkBranchAsync(commandNames, projectConfig, context, result, cancellationToken);
                    lifecycle.MarkFailed();
                    return result;
                }

                // p0128b: PlanOpenQuestionsHandler sets OpenQuestionsAwaitingAnswer when the
                // Plan emitted needs_user_input. Halt the pipeline cleanly — the run isn't a
                // failure, it's parked until the operator's reply re-triggers via webhook.
                if (context.TryGet<bool>(ContextKeys.OpenQuestionsAwaitingAnswer, out var awaiting)
                    && awaiting)
                {
                    logger.LogInformation(
                        "Pipeline parked: Plan emitted open questions; waiting on operator reply");
                    return CommandResult.Ok("Pipeline parked: awaiting_user_input");
                }

                current = advanceTo ?? batch[^1].Next;
            }

            logger.LogInformation("Pipeline completed successfully");
            return CommandResult.Ok("Pipeline completed successfully");
        }
        catch
        {
            // Any exception in setup (sandbox factory) or escaping the loop must be
            // recorded as a pipeline failure — otherwise lifecycle.DisposeAsync transitions
            // the ticket to Done instead of Failed and the operator never learns the run broke.
            lifecycle.MarkFailed();
            throw;
        }
        finally
        {
            if (sandbox is not null) await sandbox.DisposeAsync();
        }
    }

    private async Task<ISandbox> TryCreateSandboxAsync(
        ResolvedProject projectConfig,
        PipelineContext context, CancellationToken cancellationToken)
    {
        var (language, layer) = await ResolveToolchainLanguageAsync(projectConfig, context, cancellationToken);
        var spec = sandboxSpecBuilder.Build(projectConfig, language);
        // When TryCheckoutSourceHandler (api-security-scan path) cloned the source
        // host-side, attach the path so InProcessSandboxFactory uses it as workDir
        // — otherwise handlers reading from /work see an empty dir (BootstrapCheck
        // would falsely report missing context.yaml / coding-principles.md).
        if (context.TryGet<string>(ContextKeys.SourcePath, out var hostSourcePath)
            && !string.IsNullOrEmpty(hostSourcePath))
        {
            spec = spec with { InitialSourcePath = hostSourcePath };
        }
        logger.LogInformation(
            "Sandbox toolchain resolved via {Layer}: language={Language}, image={Image}",
            layer, language ?? "<none>", spec.ToolchainImage);
        var sandbox = await sandboxFactory.CreateAsync(spec, cancellationToken);
        context.Set(ContextKeys.Sandbox, sandbox);
        return sandbox;
    }

    // p0135: walk the resolution layers in priority order. Override and
    // InMemoryProjectMap are checked inline (they don't need the resolver's
    // disk/network calls); the cache + remote-context-yaml layers go through
    // SandboxLanguageResolver.
    private async Task<(string? Language, AgentSmith.Application.Services.Sandbox.SandboxToolchainResolutionLayer Layer)> ResolveToolchainLanguageAsync(
        ResolvedProject projectConfig, PipelineContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(projectConfig.Sandbox?.ToolchainImage))
        {
            // Builder consumes the override directly via ResolvedProject.Sandbox.ToolchainImage;
            // language stays null because the override is image-level.
            return (null, AgentSmith.Application.Services.Sandbox.SandboxToolchainResolutionLayer.Override);
        }

        if (context.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var inMemory)
            && !string.IsNullOrEmpty(inMemory?.PrimaryLanguage))
        {
            return (inMemory.PrimaryLanguage, AgentSmith.Application.Services.Sandbox.SandboxToolchainResolutionLayer.InMemoryProjectMap);
        }

        var currentRepo = context.Get<RepoConnection>(ContextKeys.CurrentRepo);
        var result = await sandboxLanguageResolver.ResolveAsync(currentRepo, cancellationToken);
        return (result.Language, result.Layer);
    }

    /// <summary>
    /// Best-effort persist of the WIP branch when the pipeline fails after producing
    /// local changes (p0112). Wrapped in its OWN try/catch so any persist exception
    /// can NEVER overwrite the original failure cause already in <paramref name="originalFailure"/>.
    /// </summary>
    private async Task TryPersistWorkBranchAsync(
        IReadOnlyList<string> commandNames, ResolvedProject projectConfig, PipelineContext context,
        CommandResult originalFailure, CancellationToken cancellationToken)
    {
        try
        {
            // Stamp the failed step name into context for the WIP commit's trailer block.
            context.Set(ContextKeys.FailedStepName, originalFailure.StepName);

            // Skip persist for source-less / discussion-style runs.
            if (!context.TryGet<AgentSmith.Domain.Entities.Repository>(ContextKeys.Repository, out _))
                return;

            // Skip persist for read-only pipelines (security-scan, api-security-scan, …).
            // Without a code-modifying handler in the pipeline, the workdir contains scan
            // artifacts (ZAP reports, findings JSON, …) that should NOT be staged into a
            // WIP branch. Code-modifying handlers are AgenticExecute / GenerateTests /
            // GenerateDocs — pipelines without any of those produce no source mutation.
            if (!ContainsCodeModifyingHandler(commandNames))
                return;

            var persistCmd = PipelineCommand.Simple(CommandNames.PersistWorkBranch);
            var persistContext = contextFactory.Create(persistCmd, projectConfig, context);
            var persistResult = await commandExecutor.ExecuteAsync(persistContext, cancellationToken);

            if (persistResult.IsSuccess)
                logger.LogInformation("Work branch persisted: {Message}", persistResult.Message);
            else
                logger.LogWarning("Work branch persist did not complete: {Message}", persistResult.Message);
        }
        catch (Exception ex)
        {
            // Never let a persist failure mask the original pipeline failure cause.
            logger.LogError(ex, "Work branch persist threw an exception — original failure cause preserved");
        }
    }

    private static bool ContainsCodeModifyingHandler(IReadOnlyList<string> commandNames) =>
        commandNames.Any(n => n == CommandNames.AgenticExecute
                           || n == CommandNames.GenerateTests
                           || n == CommandNames.GenerateDocs);

    internal static List<LinkedListNode<PipelineCommand>> PeelBatch(
        LinkedListNode<PipelineCommand> start, int maxConcurrent)
    {
        var batch = new List<LinkedListNode<PipelineCommand>> { start };
        if (maxConcurrent <= 1 || !IsBatchableCommand(start.Value.Name)) return batch;

        var probe = start.Next;
        while (probe is not null
               && probe.Value.Name == start.Value.Name
               && probe.Value.Round == start.Value.Round
               && IsBatchableCommand(probe.Value.Name))
        {
            batch.Add(probe);
            probe = probe.Next;
        }
        return batch;
    }

    private static bool IsBatchableCommand(string name) =>
        name is CommandNames.SkillRound
             or CommandNames.SecuritySkillRound
             or CommandNames.ApiSecuritySkillRound;

    private async Task<(CommandResult Result, LinkedListNode<PipelineCommand>? AdvanceTo)>
        ExecuteSingleStepAsync(
            LinkedListNode<PipelineCommand> current,
            LinkedList<PipelineCommand> commands,
            ResolvedProject projectConfig, PipelineContext context,
            int executionCount, CancellationToken cancellationToken)
    {
        var cmd = current.Value;
        var total = commands.Count;
        var label = CommandNames.GetLabel(cmd.Name);

        logger.LogInformation("[{Step}/{Total}] Executing {Command}...",
            executionCount, total, cmd.DisplayName);
        await progressReporter.ReportProgressAsync(executionCount, total, label, cancellationToken);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        context.Set(ContextKeys.ActivePhaseStep, cmd.Name);
        using (AttachReadGate(cmd.Name, context))
        {
            var result = await SafeExecuteAsync(cmd, projectConfig, context, cancellationToken);
            sw.Stop();
            return await FinalizeStepAsync(current, commands, projectConfig, context,
                executionCount, cmd, label, sw.Elapsed, result, cancellationToken);
        }
    }

    private IDisposable? AttachReadGate(string activeStep, PipelineContext context)
    {
        var resolved = context.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var rp)
            ? rp
            : null;
        if (resolved is null) return null;
        var flow = dataFlowResolver.Resolve(resolved.PipelineName);
        if (flow is null) return null;
        var gate = new DataFlowReadGate(
            activeStep, flow, agentSmithConfig.PipelineDataFlow.Enforce, logger);
        return context.AttachReadGate(gate);
    }

    private async Task<(CommandResult Result, LinkedListNode<PipelineCommand>? AdvanceTo)> FinalizeStepAsync(
        LinkedListNode<PipelineCommand> current, LinkedList<PipelineCommand> commands,
        ResolvedProject projectConfig, PipelineContext context, int executionCount,
        PipelineCommand cmd, string label, TimeSpan elapsed, CommandResult result,
        CancellationToken cancellationToken)
    {
        var total = commands.Count;

        context.TrackCommand(cmd.DisplayName, result.IsSuccess, result.Message,
            elapsed, result.InsertNext?.Count);

        if (!result.IsSuccess)
        {
            await ReportFailureAsync(executionCount, total, label, cmd, result,
                projectConfig, context, cancellationToken);
            return (result with { FailedStep = executionCount, TotalSteps = total, StepName = label }, null);
        }

        InsertFollowUps(current, commands, result);
        await PostSkillDetailAsync(cmd, result, cancellationToken);
        logger.LogInformation("[{Step}/{Total}] {Command} completed: {Message}",
            executionCount, commands.Count, cmd.DisplayName, result.Message);
        return (result, null);
    }

    private async Task<(CommandResult Result, LinkedListNode<PipelineCommand>? AdvanceTo)>
        ExecuteBatchStepAsync(
            IReadOnlyList<LinkedListNode<PipelineCommand>> batch,
            LinkedList<PipelineCommand> commands,
            ResolvedProject projectConfig, PipelineContext context,
            int firstStepIndex, CancellationToken cancellationToken)
    {
        var runner = new PipelineBatchRunner(commandExecutor, contextFactory, progressReporter, logger);
        var outcome = await runner.ExecuteAsync(
            batch, projectConfig, context, firstStepIndex, commands.Count, cancellationToken);

        TrackBatchedCommands(outcome, context);

        var failure = outcome.FirstFailure();
        if (failure is not null)
        {
            await ReportFailureAsync(failure.StepIndex, commands.Count,
                CommandNames.GetLabel(failure.Command.Name), failure.Command, failure.Result,
                projectConfig, context, cancellationToken);
            return (failure.Result with
            {
                FailedStep = failure.StepIndex,
                TotalSteps = commands.Count,
                StepName = CommandNames.GetLabel(failure.Command.Name)
            }, null);
        }

        var firstInsert = outcome.FirstInsertNext();
        if (firstInsert is not null)
            InsertFollowUps(firstInsert.Value.Node, commands, firstInsert.Value.Result);

        await PostBatchSkillDetailsAsync(outcome, cancellationToken);
        return (CommandResult.Ok(
            $"Batch of {batch.Count} {batch[0].Value.Name} skills (round {batch[0].Value.Round}) completed"), null);
    }

    private async Task<CommandResult> SafeExecuteAsync(
        PipelineCommand cmd, ResolvedProject projectConfig, PipelineContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var commandContext = contextFactory.Create(cmd, projectConfig, context);
            return await commandExecutor.ExecuteAsync(commandContext, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Command {Command} threw an unhandled exception", cmd.DisplayName);
            return CommandResult.Fail($"{cmd.DisplayName} failed: {ex.Message}");
        }
    }

    private static void TrackBatchedCommands(BatchOutcome outcome, PipelineContext context)
    {
        foreach (var slot in outcome.Slots)
        {
            if (slot is null) continue;
            context.TrackCommand(slot.Command.DisplayName, slot.Result.IsSuccess,
                slot.Result.Message, slot.Elapsed, slot.Result.InsertNext?.Count);
        }
    }

    private async Task PostBatchSkillDetailsAsync(BatchOutcome outcome, CancellationToken ct)
    {
        foreach (var slot in outcome.Slots)
        {
            if (slot is null) continue;
            await PostSkillDetailAsync(slot.Command, slot.Result, ct);
        }
    }

    private async Task ReportFailureAsync(
        int executionCount, int total, string label,
        PipelineCommand cmd, CommandResult result,
        ResolvedProject projectConfig, PipelineContext context, CancellationToken ct)
    {
        logger.LogWarning("Pipeline stopped at step {Step}: {Command} failed - {Message}",
            executionCount, cmd.DisplayName, result.Message);
        // HTML-formatted: AzDO System.History accepts HTML; GitHub/GitLab markdown comments
        // render inline HTML; only Jira's ADF flattens it to plain text (acceptable fallback).
        var safeMessage = System.Net.WebUtility.HtmlEncode(result.Message ?? "");
        await PostTicketStatusAsync(projectConfig, context,
            $"<b>Agent Smith — Failed</b><br/>" +
            $"<b>Step:</b> {System.Net.WebUtility.HtmlEncode(label)} ({executionCount}/{total})<br/>" +
            $"<b>Error:</b> {safeMessage}", ct);
    }

    private void InsertFollowUps(
        LinkedListNode<PipelineCommand> after,
        LinkedList<PipelineCommand> commands,
        CommandResult result)
    {
        if (result.InsertNext is not { Count: > 0 } follow) return;

        var insertAfter = after;
        foreach (var next in follow)
        {
            commands.AddAfter(insertAfter, next);
            insertAfter = insertAfter.Next!;
        }
        logger.LogInformation("{Command} inserted {Count} follow-up commands: {Commands}",
            after.Value.DisplayName, follow.Count, string.Join(", ", follow));
    }

    private async Task PostSkillDetailAsync(
        PipelineCommand cmd, CommandResult result, CancellationToken cancellationToken)
    {
        try
        {
            var detail = cmd.Name switch
            {
                CommandNames.Triage
                    => $"Triage: {result.Message}",
                CommandNames.SkillRound or CommandNames.SecuritySkillRound or CommandNames.ApiSecuritySkillRound
                    => $"Skill Round: {result.Message}",
                CommandNames.ConvergenceCheck => $"Convergence: {result.Message}",
                CommandNames.SwitchSkill => $"Skill Switch: {result.Message}",
                _ => null
            };

            if (detail is not null)
                await progressReporter.ReportDetailAsync(detail, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to post skill detail");
        }
    }

    private async Task PostTicketStatusAsync(
        ResolvedProject projectConfig, PipelineContext context,
        string message, CancellationToken cancellationToken)
    {
        try
        {
            if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId) || ticketId is null)
                return;

            var ticketProvider = ticketFactory.Create(projectConfig.Tracker);
            await ticketProvider.UpdateStatusAsync(ticketId, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to post status update to ticket");
        }
    }

}
