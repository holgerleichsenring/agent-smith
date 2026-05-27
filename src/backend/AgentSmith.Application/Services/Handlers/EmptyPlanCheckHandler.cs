using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Metrics;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0140e: post-Plan gate that short-circuits the pipeline when GeneratePlanHandler
/// produced a Plan with zero steps (and the Plan path didn't already park on open
/// questions). Sets ContextKeys.EmptyPlanSkipped so PipelineExecutor returns Ok
/// without running downstream handlers (Apply / Verify / Commit are pointless on a
/// no-op plan); emits agent_smith_pipeline_skipped_as_irrelevant_total with reason
/// label 'empty_plan'.
/// </summary>
public sealed class EmptyPlanCheckHandler(
    IEventPublisher eventPublisher,
    ILogger<EmptyPlanCheckHandler> logger)
    : ICommandHandler<EmptyPlanCheckContext>
{
    private const string EmptyPlanReason = "empty_plan";
    private const string GateName = "empty_plan";

    public async Task<CommandResult> ExecuteAsync(
        EmptyPlanCheckContext context, CancellationToken cancellationToken)
    {
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var r) ? r : null;

        if (!context.Pipeline.TryGet<Plan>(ContextKeys.Plan, out var plan) || plan is null)
        {
            await PublishGateAsync(runId, passed: true, "no plan in context", cancellationToken);
            return CommandResult.Ok("empty-plan-check: no Plan in context, skipping gate");
        }

        if (plan.Steps.Count > 0)
        {
            await PublishGateAsync(runId, passed: true, $"plan has {plan.Steps.Count} step(s)", cancellationToken);
            return CommandResult.Ok(
                $"empty-plan-check: plan has {plan.Steps.Count} step(s) — continuing");
        }

        var (projectName, pipelineName) = ResolveLabels(context.Pipeline);
        AgentSmithMeter.PipelineSkippedAsIrrelevant.Add(1,
            new KeyValuePair<string, object?>("project", projectName),
            new KeyValuePair<string, object?>("pipeline", pipelineName),
            new KeyValuePair<string, object?>("reason", EmptyPlanReason));

        context.Pipeline.Set(ContextKeys.EmptyPlanSkipped, true);
        var ticketLabel = context.Pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var t) ? t!.Value : "n/a";
        logger.LogInformation(
            "empty_plan_skip: project={Project} pipeline={Pipeline} ticket={Ticket} reason={Reason} — pipeline will exit cleanly",
            projectName, pipelineName, ticketLabel, EmptyPlanReason);

        await PublishGateAsync(runId, passed: false, EmptyPlanReason, cancellationToken);
        return CommandResult.Ok($"empty-plan-skip: reason={EmptyPlanReason}");
    }

    private Task PublishGateAsync(string? runId, bool passed, string reason, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(runId)) return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new GateCheckedEvent(runId!, GateName, passed, reason, DateTimeOffset.UtcNow), ct);
    }

    private static (string Project, string Pipeline) ResolveLabels(PipelineContext pipeline)
    {
        var pipelineName = pipeline.TryGet<ResolvedPipelineConfig>(ContextKeys.ResolvedPipeline, out var rp) && rp is not null
            ? rp.PipelineName
            : "unknown";
        // No project-name context-key exists today; use ResolvedPipeline.PipelineName for both
        // labels until a project-name slot is introduced. The metric still produces useful
        // dashboards on (pipeline, reason); project label can be tightened in a follow-up.
        var projectName = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) && ticket is not null
            ? ticket.Source
            : "unknown";
        return (projectName, pipelineName);
    }
}
