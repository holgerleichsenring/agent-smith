using System.Security.Cryptography;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: SemaphoreSlim(MaxConcurrentSubAgents) + Task.WhenAll over the
/// SubAgentSpec list. Per spec: emit Spawned event, run the agentic loop
/// via IAgenticLoopRunner, emit Completed event, map to SubAgentResult.
/// Returns results in deterministic spec order regardless of completion
/// order — the merge index is the slot in the input list, not the time
/// of arrival. NO fail-fast: a failed child returns Status=Failed and the
/// siblings keep running.
/// </summary>
public sealed class SubAgentRunner(
    IAgenticLoopRunner loopRunner,
    IEventPublisher eventPublisher,
    LoopLimitsConfig loopLimits,
    ILogger<SubAgentRunner> logger) : ISubAgentRunner
{
    public async Task<IReadOnlyList<SubAgentResult>> RunAsync(
        IReadOnlyList<SubAgentSpec> specs,
        SubAgentContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(context);
        if (specs.Count == 0) return Array.Empty<SubAgentResult>();

        var maxConcurrent = Math.Max(1, loopLimits.MaxConcurrentSubAgents);
        using var throttle = new SemaphoreSlim(maxConcurrent);
        var slots = new SubAgentResult?[specs.Count];

        var tasks = specs.Select((spec, index) => RunOneAsync(
            spec, index, context, throttle, slots, cancellationToken)).ToArray();
        await Task.WhenAll(tasks);

        return slots.Select(s => s!).ToArray();
    }

    private async Task RunOneAsync(
        SubAgentSpec spec, int index, SubAgentContext context,
        SemaphoreSlim throttle, SubAgentResult?[] slots, CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        try
        {
            slots[index] = await ExecuteAsync(spec, index, context, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sub-agent slot {Index} threw outside the loop runner — recording as failed", index);
            slots[index] = BuildFailureResult(spec, index, Guid.NewGuid().ToString("N"), 0m);
        }
        finally
        {
            throttle.Release();
        }
    }

    private async Task<SubAgentResult> ExecuteAsync(
        SubAgentSpec spec, int index, SubAgentContext context, CancellationToken ct)
    {
        var subAgentId = $"sa-{Guid.NewGuid():N}".Substring(0, 8);
        var contextHash = HashInheritedContext(spec.InheritedContext);
        await PublishSpawnedAsync(spec, subAgentId, context, contextHash, ct);

        try
        {
            var request = BuildLoopRequest(spec, subAgentId, context);
            var loopResult = await loopRunner.RunAsync(request, ct);

            // Each child's cost rolls up against the shared per-run tracker.
            context.CostTracker.Track(loopResult.Response);
            var costUsd = EstimateCostUsd(loopResult.Response);
            var observationsCount = CountObservations(loopResult.Response);
            var toolCalls = CountToolCalls(loopResult.Response);

            await PublishCompletedAsync(
                subAgentId, context, SubAgentStatus.Succeeded,
                observationsCount, findings: 0, files: 0, toolCalls, costUsd, ct);

            return new SubAgentResult(
                TaskIndex: index,
                Status: SubAgentStatus.Succeeded,
                SubAgentId: subAgentId,
                Name: spec.Name,
                ObservationsCount: observationsCount,
                FindingsCount: 0,
                FilesWrittenCount: 0,
                ToolCalls: toolCalls,
                CostUsd: costUsd,
                OccurredAt: DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sub-agent {SubAgentId} ({Name}) failed inside the loop runner", subAgentId, spec.Name);
            await PublishCompletedAsync(
                subAgentId, context, SubAgentStatus.Failed,
                0, 0, 0, 0, 0m, ct);
            return BuildFailureResult(spec, index, subAgentId, 0m);
        }
    }

    private AgenticLoopRequest BuildLoopRequest(
        SubAgentSpec spec, string subAgentId, SubAgentContext context)
    {
        var pipelineName = ResolvePipelineName(context.Pipeline);
        var tools = context.ToolKit.GetToolsFor(
            pipelineName, SkillExecutionPhase.Implementation, investigatorMode: null,
            hosts: Array.Empty<Tools.IToolHost>());
        var systemPrompt = BuildSystemPrompt(spec);
        var userPrompt = BuildUserPrompt(spec);
        return new AgenticLoopRequest(
            AgentConfig: ResolveAgentConfig(context.Pipeline),
            TaskType: TaskType.Primary,
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            Tools: tools,
            InheritedContext: spec.InheritedContext,
            Name: spec.Name,
            Activity: spec.Activity,
            SubAgentId: subAgentId,
            ParentSubAgentId: context.ParentSubAgentId);
    }

    private static AgentConfig ResolveAgentConfig(PipelineContext pipeline)
    {
        if (pipeline.TryGet<AgentConfig>(ContextKeys.AgentConfig, out var agent) && agent is not null)
            return agent;
        return new AgentConfig();
    }

    private static string ResolvePipelineName(PipelineContext pipeline)
    {
        if (pipeline.TryGet<Contracts.Models.Configuration.ResolvedPipelineConfig>(
                ContextKeys.ResolvedPipeline, out var resolved) && resolved is not null)
            return resolved.PipelineName;
        return Tools.IToolKit.WildcardPipelineName;
    }

    private static string BuildSystemPrompt(SubAgentSpec spec)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are {spec.Name}.");
        sb.AppendLine($"Activity: {spec.Activity}");
        if (!string.IsNullOrWhiteSpace(spec.InheritedContext.OptionalSystemPromptBlock))
            sb.AppendLine(spec.InheritedContext.OptionalSystemPromptBlock);
        return sb.ToString();
    }

    private static string BuildUserPrompt(SubAgentSpec spec)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pipeline goal: {spec.InheritedContext.PipelineGoal}");
        sb.AppendLine();
        sb.AppendLine("Prior context:");
        sb.AppendLine(spec.InheritedContext.PriorContextSlice);
        sb.AppendLine();
        sb.AppendLine($"Your task: {spec.TaskDescription}");
        if (!string.IsNullOrWhiteSpace(spec.OutputHint))
        {
            sb.AppendLine();
            sb.AppendLine($"Output hint: {spec.OutputHint}");
        }
        return sb.ToString();
    }

    private Task PublishSpawnedAsync(
        SubAgentSpec spec, string subAgentId, SubAgentContext context,
        string contextHash, CancellationToken ct)
    {
        var evt = new SubAgentSpawnedEvent(
            context.MasterRunId, subAgentId, spec.Name, spec.Activity,
            context.ParentSubAgentId, contextHash, DateTimeOffset.UtcNow);
        return eventPublisher.PublishAsync(evt, ct);
    }

    private Task PublishCompletedAsync(
        string subAgentId, SubAgentContext context, SubAgentStatus status,
        int observations, int findings, int files, int toolCalls, decimal costUsd,
        CancellationToken ct)
    {
        var evt = new SubAgentCompletedEvent(
            context.MasterRunId, subAgentId, status.ToString(),
            observations, findings, files, toolCalls, costUsd, DateTimeOffset.UtcNow);
        return eventPublisher.PublishAsync(evt, ct);
    }

    private static SubAgentResult BuildFailureResult(SubAgentSpec spec, int index, string subAgentId, decimal cost) =>
        new(
            TaskIndex: index,
            Status: SubAgentStatus.Failed,
            SubAgentId: subAgentId,
            Name: spec.Name,
            ObservationsCount: 0,
            FindingsCount: 0,
            FilesWrittenCount: 0,
            ToolCalls: 0,
            CostUsd: cost,
            OccurredAt: DateTimeOffset.UtcNow);

    private static string HashInheritedContext(InheritedContext ctx)
    {
        var payload = $"{ctx.PipelineGoal}\n{ctx.PriorContextSlice}\n{ctx.OptionalSystemPromptBlock}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return "sha256:" + Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private static decimal EstimateCostUsd(ChatResponse response)
    {
        var usage = response.Usage;
        if (usage is null) return 0m;
        return 0m;
    }

    private static int CountObservations(ChatResponse response) =>
        response.Messages?.Count(m => m.Role == ChatRole.Assistant) ?? 0;

    private static int CountToolCalls(ChatResponse response) =>
        response.Messages?.Sum(m => m.Contents?.OfType<FunctionCallContent>().Count() ?? 0) ?? 0;
}
