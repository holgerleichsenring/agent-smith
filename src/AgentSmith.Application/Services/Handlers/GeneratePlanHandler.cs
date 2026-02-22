using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Generates an execution plan using the AI agent provider.
/// </summary>
public sealed class GeneratePlanHandler(
    IAgentProviderFactory factory,
    ILogger<GeneratePlanHandler> logger)
    : ICommandHandler<GeneratePlanContext>
{
    public async Task<CommandResult> ExecuteAsync(
        GeneratePlanContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Generating plan for ticket {Ticket}...", context.Ticket.Id);

        var provider = factory.Create(context.AgentConfig);
        var plan = await provider.GeneratePlanAsync(
            context.Ticket, context.CodeAnalysis, context.CodingPrinciples, cancellationToken);

        context.Pipeline.Set(ContextKeys.Plan, plan);

        logger.LogInformation(
            "Plan generated: {Summary} ({Steps} steps)",
            plan.Summary, plan.Steps.Count);

        return CommandResult.Ok($"Plan generated with {plan.Steps.Count} steps");
    }
}
