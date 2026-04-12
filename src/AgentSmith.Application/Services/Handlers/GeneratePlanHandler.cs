using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates an execution plan using the AI agent provider.
/// Writes plan decisions to the decision log after parsing.
/// </summary>
public sealed class GeneratePlanHandler(
    IAgentProviderFactory factory,
    IDecisionLogger decisionLogger,
    ILogger<GeneratePlanHandler> logger)
    : ICommandHandler<GeneratePlanContext>
{
    public async Task<CommandResult> ExecuteAsync(
        GeneratePlanContext context, CancellationToken cancellationToken)
    {
        if (context.Pipeline.Has(ContextKeys.ConsolidatedPlan))
        {
            logger.LogInformation("Plan already consolidated by multi-role discussion, skipping generation");
            return CommandResult.Ok("Plan consolidated by multi-role discussion");
        }

        logger.LogInformation("Generating plan for ticket {Ticket}...", context.Ticket.Id);

        var provider = factory.Create(context.AgentConfig);
        var plan = await provider.GeneratePlanAsync(
            context.Ticket, context.CodeAnalysis, context.CodingPrinciples,
            context.CodeMap, context.ProjectContext, cancellationToken);

        context.Pipeline.Set(ContextKeys.Plan, plan);

        if (plan.Decisions.Count > 0)
        {
            context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo);
            var sourceLabel = $"#{context.Ticket.Id}";
            await WriteDecisionsAsync(repo?.LocalPath, plan.Decisions, sourceLabel, cancellationToken);
            context.Pipeline.AppendDecisions(plan.Decisions);
        }

        logger.LogInformation(
            "Plan generated: {Summary} ({Steps} steps, {Decisions} decisions)",
            plan.Summary, plan.Steps.Count, plan.Decisions.Count);

        return CommandResult.Ok($"Plan generated with {plan.Steps.Count} steps");
    }

    private async Task WriteDecisionsAsync(
        string? repoPath, IReadOnlyList<PlanDecision> decisions,
        string? sourceLabel, CancellationToken cancellationToken)
    {
        foreach (var d in decisions)
        {
            if (Enum.TryParse<DecisionCategory>(d.Category, true, out var cat))
                await decisionLogger.LogAsync(repoPath, cat, d.Decision, cancellationToken, sourceLabel);
            else
                logger.LogWarning("Unknown decision category '{Category}', skipping", d.Category);
        }
    }
}
