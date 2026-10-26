using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-26-bdd0c: a spec derivation's hand-back. The question goes on the ticket as a plain comment, which the
/// ticket conversation shows with the rest of the thread; the ticket keeps its status and no run waits for the
/// answer. The run ends on <see cref="ContextKeys.SpecDerivationEnded"/>, a success, never a park, and the
/// pointer is left alone: its repeat guard and heads belong to code attempts.
/// </summary>
public sealed class DerivationQuestion(
    ITicketProviderFactory tickets,
    SpecSetAnnouncer announcer,
    IEventPublisher events,
    IRunContextAccessor runContext,
    ILogger<DerivationQuestion> logger)
{
    /// <summary>What the comment says after the question.</summary>
    public const string Next = "Answer on this ticket, then derive the specs again.";

    public async Task<CommandResult> AskAsync(SpecHandbackContext context, SpecHandback handback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(handback);
        var waiting = $"{TicketMention.WaitingLine(context.Tracker!.Type, context.Ticket)} {Next}";
        var comment = SpecHandbackComment.Build(handback, SpecPullRequests.Of(context.Pipeline), waiting);
        await tickets.Create(context.Tracker).UpdateStatusAsync(context.Ticket!.Id, comment, ct);
        await PublishAsync(comment, handback.Quote, ct);
        await announcer.AnnounceHandbackAsync(
            context.Pipeline, TicketKeyFactory.For(context.Ticket, context.Pipeline).Value, handback, ct);
        var ended = $"Spec derivation stopped: {handback.Reason}";
        context.Pipeline.Set(ContextKeys.SpecDerivationEnded, ended);
        logger.LogInformation("Spec derivation of ticket {Ticket} asked on the ticket ({Case})",
            context.Ticket.Id.Value, handback.Case);
        return CommandResult.Ok(ended);
    }

    private async Task PublishAsync(string comment, string? quote, CancellationToken ct)
    {
        if (runContext.CurrentRunId is not { Length: > 0 } runId) return;
        try
        {
            await events.PublishAsync(new SpecHandedBackEvent(runId, DateTimeOffset.UtcNow, comment, quote), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "The question of derivation {RunId} was posted but not published", runId);
        }
    }
}
