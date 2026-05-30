using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Requests user approval before code-changing work begins. Renders
/// either the structured Plan steps (legacy choreography presets) or
/// the ticket + repos summary (collapsed coding presets after p0179b,
/// where the agentic master owns planning internally).
/// Headless mode auto-approves regardless of which branch ran.
/// </summary>
public sealed class ApprovalHandler(
    ILogger<ApprovalHandler> logger)
    : ICommandHandler<ApprovalContext>
{
    public Task<CommandResult> ExecuteAsync(
        ApprovalContext context, CancellationToken cancellationToken)
    {
        if (context.Plan is not null)
        {
            logger.LogInformation("Plan summary: {Summary}", context.Plan.Summary);
            DisplayPlan(context.Plan);
        }
        else
        {
            DisplayTicketSummary(context);
        }

        var headless = context.Pipeline.TryGet<bool>(ContextKeys.Headless, out var h) && h;

        bool approved;
        if (headless)
        {
            logger.LogInformation("Headless mode: auto-approving");
            approved = true;
        }
        else
        {
            Console.Write("Approve? (y/n): ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();
            approved = input is "y" or "yes";
        }

        context.Pipeline.Set(ContextKeys.Approved, approved);

        return Task.FromResult(approved
            ? CommandResult.Ok("approved by user")
            : CommandResult.Fail("rejected by user"));
    }

    private void DisplayTicketSummary(ApprovalContext context)
    {
        if (context.Pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) && ticket is not null)
        {
            logger.LogInformation(
                "Master will work on ticket {Id}: {Title}", ticket.Id.Value, ticket.Title);
            Console.WriteLine($"  Ticket: {ticket.Id.Value} — {ticket.Title}");
        }
        if (context.Pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is not null)
        {
            Console.WriteLine($"  Repos: {string.Join(", ", repos.Select(r => r.Name))}");
        }
    }

    private static void DisplayPlan(Domain.Entities.Plan plan)
    {
        foreach (var step in plan.Steps)
            Console.WriteLine($"  [{step.Order}] {step.ChangeType}: {step.Description}");
    }
}
