using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Requests user approval before code-changing work begins. Headless mode
/// auto-approves (unchanged — every ticket-poller run is headless today).
/// p0327: the interactive path routes through the dialogue ask gate instead of
/// a raw Console.ReadLine — the console transport renders the same prompt for
/// CLI runs, chat/dashboard transports reach remote operators, and a ticket
/// run whose approval outlives the hot window checkpoints and parks until the
/// answer (or the days-scale timeout's "reject" default) arrives.
/// </summary>
public sealed class ApprovalHandler(
    IDialogueAskGate askGate,
    ILogger<ApprovalHandler> logger)
    : ICommandHandler<ApprovalContext>
{
    private const int DefaultApprovalTimeoutSeconds = 259_200; // 3 days
    private static readonly string[] ApprovalAnswers = ["y", "yes", "approve", "approved", "ok"];

    public async Task<CommandResult> ExecuteAsync(
        ApprovalContext context, CancellationToken cancellationToken)
    {
        if (context.Pipeline.TryGet<bool>(ContextKeys.Headless, out var headless) && headless)
        {
            logger.LogInformation("Headless mode: auto-approving");
            context.Pipeline.Set(ContextKeys.Approved, true);
            return CommandResult.Ok("approved (headless)");
        }

        var outcome = await askGate.AskAsync(
            context.Pipeline, BuildQuestion(context), cancellationToken);
        if (outcome.Checkpointed)
            return CommandResult.Ok("Approval parked: waiting for the operator (checkpointed)");

        var approved = ApprovalAnswers.Contains(outcome.Answer!.Answer.Trim().ToLowerInvariant());
        context.Pipeline.Set(ContextKeys.Approved, approved);
        return approved
            ? CommandResult.Ok($"approved by {outcome.Answer.AnsweredBy}")
            : CommandResult.Fail($"rejected by {outcome.Answer.AnsweredBy}: {outcome.Answer.Answer}");
    }

    private static DialogQuestion BuildQuestion(ApprovalContext context) => new(
        QuestionId: Guid.NewGuid().ToString("N"),
        Type: QuestionType.Approval,
        Text: "Approve this plan? Agent Smith will start changing code once approved.",
        Context: BuildSummary(context),
        Choices: null,
        // Silence must never approve code changes — the timeout default rejects.
        DefaultAnswer: "reject",
        Timeout: TimeSpan.FromSeconds(ResolveTimeoutSeconds(context.Pipeline)));

    private static int ResolveTimeoutSeconds(PipelineContext pipeline) =>
        pipeline.TryGet<int>(ContextKeys.DialogueApprovalTimeoutSeconds, out var seconds) && seconds > 0
            ? seconds
            : DefaultApprovalTimeoutSeconds;

    private static string BuildSummary(ApprovalContext context)
    {
        if (context.Plan is not null)
        {
            var steps = context.Plan.Steps
                .Select(s => $"[{s.Order}] {s.ChangeType}: {s.Description}");
            return $"{context.Plan.Summary}\n{string.Join("\n", steps)}";
        }

        var parts = new List<string>();
        if (context.Pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) && ticket is not null)
            parts.Add($"Ticket {ticket.Id.Value} — {ticket.Title}");
        if (context.Pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is not null)
            parts.Add($"Repos: {string.Join(", ", repos.Select(r => r.Name))}");
        return string.Join("\n", parts);
    }
}
