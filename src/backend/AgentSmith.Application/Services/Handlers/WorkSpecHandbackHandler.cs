using AgentSmith.Application.Models;
using AgentSmith.Application.Services.WorkSpecs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0390: routes the three hand-back cases. not-understood and
/// requirements-do-not-match-the-code park where p0318 parks and re-trigger on an
/// answer; not-implementable is a VERDICT — it parks in its own status, does not
/// auto-retry on a comment, and restarts only on an explicit operator Retry.
/// Two hand-backs with the same case code and no source commit between them end
/// the loop: the run proceeds instead of parking again, because a signal that
/// fires forever teaches the operator to ignore it.
/// </summary>
public sealed class WorkSpecHandbackHandler(
    ITicketProviderFactory ticketFactory,
    WorkSpecParkStatusResolver parkStatus,
    IWorkSpecPointerStore pointers,
    ILogger<WorkSpecHandbackHandler> logger)
    : ICommandHandler<WorkSpecHandbackContext>
{
    public async Task<CommandResult> ExecuteAsync(
        WorkSpecHandbackContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<WorkSpecHandback>(ContextKeys.WorkSpecHandback, out var handback)
            || handback is null || handback.Case == WorkSpecHandbackCase.None)
            return CommandResult.Ok("The work spec handed nothing back");
        if (context.Ticket is null || context.Tracker is null)
            return CommandResult.Ok($"Work spec handed back ({handback.Case}) but the run has no tracker");

        var project = context.Pipeline.TryGet<string>(ContextKeys.ProjectName, out var p) ? p! : string.Empty;
        var key = WorkSpecKeyFactory.For(context.Ticket, context.Pipeline).Value;
        var pointer = await pointers.GetAsync(project, key, cancellationToken);
        var head = pointer?.RevisionSha ?? string.Empty;
        if (WorkSpecHandbackProgress.RepeatsWithoutProgress(pointer, handback.Case, head))
        {
            logger.LogWarning(
                "Work spec {Key} handed back '{Case}' again with nothing committed since — "
                + "not parking a second time", key, handback.Case);
            return CommandResult.Ok(
                $"Work spec handed back '{handback.Case}' again with no change since the last "
                + "hand-back — the loop ends here and the run continues");
        }

        await ParkAsync(context, handback, cancellationToken);
        if (pointer is not null)
            await pointers.SaveAsync(project,
                WorkSpecHandbackProgress.Record(pointer, handback.Case, head), cancellationToken);
        return Result(handback);
    }

    private async Task ParkAsync(
        WorkSpecHandbackContext context, WorkSpecHandback handback, CancellationToken ct)
    {
        var status = parkStatus.Resolve(context.Pipeline, context.Tracker!, handback.Case);
        var prUrl = context.Pipeline.TryGet<string>(ContextKeys.WorkSpecPullRequestUrl, out var url)
            ? url : null;
        await ticketFactory.Create(context.Tracker!).FinalizeAsync(
            context.Ticket!.Id, WorkSpecHandbackComment.Build(handback, prUrl), status, ct);
        // The awaiting-answer flag short-circuits the rest of the run for BOTH classes:
        // there is nothing to build either way. What differs is how the ticket comes
        // back — an answered question re-triggers, a verdict waits for a Retry.
        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);
        logger.LogInformation(
            "Work spec handed ticket {Ticket} back ({Case}) — parked in {Status}",
            context.Ticket.Id.Value, handback.Case, status);
    }

    private static CommandResult Result(WorkSpecHandback handback) =>
        handback.IsVerdict
            ? CommandResult.Ok(
                $"awaiting_user_input: not implementable as specified — {handback.Reason}")
            : CommandResult.Ok($"awaiting_user_input: handed back ({handback.Case})");
}
