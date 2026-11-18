using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-5701b: the first step of a 'file' run — the approved proposal as an ordered <see cref="FilingPlan"/>.
/// A refusal (a list ticket without a spec, a requires cycle, a required ticket the tracker lacks) fails the step
/// before anything exists. A run with no request fails with a message and never throws.
/// </summary>
public sealed class PlanFilingHandler(FilingPlanner planner, ILogger<PlanFilingHandler> logger)
    : ICommandHandler<PlanFilingContext>
{
    public async Task<CommandResult> ExecuteAsync(PlanFilingContext context, CancellationToken cancellationToken)
    {
        if ((!context.Pipeline.TryGet<FilingRequest>(ContextKeys.FilingRequest, out var request) || request is null)
            && context.Pipeline.Has(ContextKeys.SeriesRunId)) // 2026-11-17-0207d: a series check with nothing failing files nothing
            return CommandResult.Ok("Nothing to file: the series check found no new failure.");
        if (request is null)
            return CommandResult.Fail("A 'file' run files an approval, and this run carries none: the server seeds "
                + $"ContextKeys.{nameof(ContextKeys.FilingRequest)} when a person approves. Nothing was filed.");
        try
        {
            var plan = await planner.PlanAsync(request, context.Project, cancellationToken);
            context.Pipeline.Set(ContextKeys.FilingPlan, plan);
            return CommandResult.Ok($"Planned {plan.Tickets.Count} ticket(s) for project '{context.Project.Name}'");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The filing of conversation {Conversation} was refused", request.Conversation);
            return CommandResult.Fail(ex.Message);
        }
    }
}
