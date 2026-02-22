using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

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

        var headless = context.Pipeline.TryGet<bool>(ContextKeys.Headless, out var h) && h;

        bool approved;
        if (headless)
        {
            logger.LogInformation("Headless mode: auto-approving plan");
            approved = true;
        }
        else
        {
            Console.Write("Approve this plan? (y/n): ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();
            approved = input is "y" or "yes";
        }

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
