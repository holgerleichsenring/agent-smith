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
        // ConsolidatedPlan from multi-skill discussion is additional context
        // for plan generation, not a replacement. The discussion provides analysis
        // and recommendations; this handler distills them into concrete PlanSteps.
        var projectContext = context.ProjectContext;
        if (context.Pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var consolidated)
            && consolidated is not null)
        {
            logger.LogInformation("Including consolidated multi-role discussion as plan context");
            projectContext = string.IsNullOrEmpty(projectContext)
                ? $"## Multi-Role Discussion\n\n{consolidated}"
                : $"{projectContext}\n\n## Multi-Role Discussion\n\n{consolidated}";
        }

        logger.LogInformation("Generating plan for ticket {Ticket}...", context.Ticket.Id);

        var provider = factory.Create(context.AgentConfig);
        var plan = await provider.GeneratePlanAsync(
            context.Ticket, context.CodeAnalysis, context.CodingPrinciples,
            context.CodeMap, projectContext, cancellationToken);

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
