using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: routes the two hand-back cases. A requirement that contradicts the
/// repository parks where p0318 parks and re-triggers on an answer;
/// not-implementable is a VERDICT — it parks in its own status, does not auto-retry
/// on a comment, and restarts only on an explicit operator Retry.
/// <para>
/// Two hand-backs with the same case code and no source commit between them end the
/// loop: the run proceeds instead of parking again, because a signal that fires
/// forever teaches the operator to ignore it.
/// </para>
/// </summary>
public sealed class SpecHandbackHandler(
    ITicketProviderFactory ticketFactory,
    SpecParkStatusResolver parkStatus,
    ISpecSetPointerStore pointers,
    ILogger<SpecHandbackHandler> logger)
    : ICommandHandler<SpecHandbackContext>
{
    public async Task<CommandResult> ExecuteAsync(
        SpecHandbackContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<SpecHandback>(ContextKeys.SpecHandback, out var handback)
            || handback is null || handback.Case == SpecHandbackCase.None)
            return CommandResult.Ok("The derivation handed nothing back");
        if (context.Ticket is null || context.Tracker is null)
            return CommandResult.Ok($"Derivation handed back ({handback.Case}) but the run has no tracker");

        var project = context.Pipeline.TryGet<string>(ContextKeys.ProjectName, out var p) ? p! : string.Empty;
        var key = SpecSetKeyFactory.For(context.Ticket, context.Pipeline).Value;
        var pointer = await pointers.GetAsync(project, key, cancellationToken);
        var head = pointer?.RevisionSha ?? string.Empty;
        if (SpecHandbackProgress.RepeatsWithoutProgress(pointer, handback.Case, head))
        {
            logger.LogWarning(
                "Spec {Key} handed back '{Case}' again with nothing committed since — "
                + "not parking a second time", key, handback.Case);
            return CommandResult.Ok(
                $"The derivation handed back '{handback.Case}' again with no change since the last "
                + "hand-back — the loop ends here and the run continues");
        }

        // p0391a: an unresolvable park status is the RUN's failure, not a park in the wrong
        // place — handing back while the ticket keeps a claimable status would re-trigger it.
        var status = parkStatus.TryResolve(context.Pipeline, context.Tracker!, handback.Case);
        if (string.IsNullOrWhiteSpace(status))
        {
            logger.LogError("The derivation cannot park: {Reason}", parkStatus.UnresolvedReason);
            return CommandResult.Fail(parkStatus.UnresolvedReason);
        }

        await ParkAsync(context, handback, status!, cancellationToken);
        if (pointer is not null)
            await pointers.SaveAsync(project,
                SpecHandbackProgress.Record(pointer, handback.Case, head), cancellationToken);
        return Result(handback);
    }

    private async Task ParkAsync(
        SpecHandbackContext context, SpecHandback handback, string status, CancellationToken ct)
    {
        var prUrl = context.Pipeline.TryGet<string>(ContextKeys.SpecPullRequestUrl, out var url)
            ? url : null;
        await ticketFactory.Create(context.Tracker!).FinalizeAsync(
            context.Ticket!.Id, SpecHandbackComment.Build(handback, prUrl), status, ct);
        // The awaiting-answer flag short-circuits the rest of the run for BOTH classes:
        // there is nothing to build either way. What differs is how the ticket comes back —
        // an answered question re-triggers, a verdict waits for a Retry.
        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);
        logger.LogInformation(
            "The derivation handed ticket {Ticket} back ({Case}) — parked in {Status}",
            context.Ticket.Id.Value, handback.Case, status);
    }

    private static CommandResult Result(SpecHandback handback) =>
        handback.IsVerdict
            ? CommandResult.Ok(
                $"awaiting_user_input: not implementable as specified — {handback.Reason}")
            : CommandResult.Ok($"awaiting_user_input: handed back ({handback.Case})");
}
