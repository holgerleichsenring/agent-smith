using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Commands.Handlers;

/// <summary>
/// Requests user approval for an execution plan via CLI prompt.
/// Fully implemented - no provider dependency.
/// </summary>
public sealed class ApprovalHandler(
    ILogger<ApprovalHandler> logger)
    : ICommandHandler<ApprovalContext>
{
    public Task<CommandResult> ExecuteAsync(
        ApprovalContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Plan summary: {Summary}", context.Plan.Summary);
        DisplayPlan(context.Plan);

        Console.Write("Approve this plan? (y/n): ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        var approved = input is "y" or "yes";

        context.Pipeline.Set(ContextKeys.Approved, approved);

        return Task.FromResult(approved
            ? CommandResult.Ok("Plan approved by user")
            : CommandResult.Fail("Plan rejected by user"));
    }

    private static void DisplayPlan(Domain.Entities.Plan plan)
    {
        foreach (var step in plan.Steps)
            Console.WriteLine($"  [{step.Order}] {step.ChangeType}: {step.Description}");
    }
}
