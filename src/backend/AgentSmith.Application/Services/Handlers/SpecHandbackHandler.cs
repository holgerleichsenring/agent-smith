using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393a: routes the two hand-back cases. A requirement that contradicts the
/// repository parks where p0318 parks and re-triggers on an answer;
/// not-implementable is a VERDICT — it parks in its own status, does not auto-retry
/// on a comment, and restarts only on an explicit operator Retry.
/// <para>
/// Two contradictions in a row with no reply on the ticket between them end the loop,
/// because a signal that fires forever teaches the operator to ignore it.
/// <see cref="SpecHandbackRepeat"/> reads the thread. The loop ends as a FAILED step,
/// not a continue: a hand-back carries no phases, and a run that continues past one
/// reaches CommitAndPR with the spec draft already pushed, which closes the ticket as
/// completed with nothing built (2026-09-07-bd7a).
/// </para>
/// <para>
/// A REFUSAL (raised by ScopeRepos, which splices this step in behind itself) parks
/// like the contradiction case and is excluded from that loop-ending: continuing past
/// what must not be done is the one wrong answer. With no tracker to park on it ends
/// the run as a failed step — the continue branch would run checkout.
/// </para>
/// <para>
/// A QUESTION parks like the contradiction case every time it is raised; its loop is
/// ended by the conversation, not here: an unanswered question is pinned into the next
/// derivation as the answer, so a second park is the model asking again.
/// </para>
/// </summary>
public sealed class SpecHandbackHandler(
    ITicketProviderFactory ticketFactory,
    SpecParkStatusResolver parkStatus,
    ISpecSetPointerStore pointers,
    SpecHandbackRepeat repeat,
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
            return NoTracker(handback);

        var project = context.Pipeline.TryGet<string>(ContextKeys.ProjectName, out var p) ? p! : string.Empty;
        var key = SpecSetKeyFactory.For(context.Ticket, context.Pipeline).Value;
        var pointer = await pointers.GetAsync(project, key, cancellationToken);
        if (repeat.IsRepeat(pointer, handback.Case, context.Pipeline))
        {
            logger.LogWarning(
                "Spec {Key} handed back '{Case}' again with no reply on the ticket since — "
                + "not parking a second time", key, handback.Case);
            return CommandResult.Fail(
                $"The derivation handed back '{handback.Case}' again with no reply since the last "
                + $"hand-back: {handback.Reason} — the loop ends here. Reply on the ticket with what "
                + "changes the picture and move it back to a trigger status.");
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
                SpecHandbackProgress.Record(pointer, handback.Case), cancellationToken);
        return Result(handback);
    }

    private async Task ParkAsync(
        SpecHandbackContext context, SpecHandback handback, string status, CancellationToken ct)
    {
        var prUrl = context.Pipeline.TryGet<string>(ContextKeys.SpecPullRequestUrl, out var url)
            ? url : null;
        var waitingLine = TicketMention.WaitingLine(context.Tracker!.Type, context.Ticket);
        await ticketFactory.Create(context.Tracker!).FinalizeAsync(
            context.Ticket!.Id, SpecHandbackComment.Build(handback, prUrl, waitingLine), status, ct);
        // The awaiting-answer flag short-circuits the rest of the run for BOTH classes:
        // there is nothing to build either way. What differs is how the ticket comes back —
        // an answered question re-triggers, a verdict waits for a Retry.
        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);
        logger.LogInformation(
            "Ticket {Ticket} handed back ({Case}) — parked in {Status}",
            context.Ticket.Id.Value, handback.Case, status);
    }

    private static CommandResult Result(SpecHandback handback) => handback.Case switch
    {
        SpecHandbackCase.NotImplementable => CommandResult.Ok(
            $"awaiting_user_input: not implementable as specified — {handback.Reason}"),
        SpecHandbackCase.Refused => CommandResult.Ok(
            $"awaiting_user_input: refused — {handback.Reason}"),
        SpecHandbackCase.Question => CommandResult.Ok(
            $"awaiting_user_input: the ticket reads two ways — {handback.Reason}"),
        _ => CommandResult.Ok($"awaiting_user_input: handed back ({handback.Case})"),
    };

    private static CommandResult NoTracker(SpecHandback handback) =>
        handback.Case == SpecHandbackCase.Refused
            ? CommandResult.Fail(
                $"Refused: {handback.Reason} — quoted: \"{handback.Quote}\" — "
                + "and the run has no tracker to park the ticket on")
            : CommandResult.Ok($"Derivation handed back ({handback.Case}) but the run has no tracker");
}
