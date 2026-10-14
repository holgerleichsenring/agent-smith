using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The bound ticket's next attempt, asked for in its conversation by the turn's poster. A series edit no attempt
/// has worked starts as its approval (<see cref="PendingEditStart"/>); with none, the latest attempt is retried
/// unchanged from the conversation (<see cref="RunRetry"/>), whose gate admits any end but a delivered success.
/// Each ask runs in its own scope, apart from the turn's unit of work; a ticket waiting for an unlanded one queues nothing.
/// </summary>
public sealed class ConversationAttemptStart(
    IServiceScopeFactory scopes, ResolvedProject project, string ticketId, string conversation, string poster,
    ILogger<ConversationAttemptStart> logger) : IAttemptStart
{
    public const string Requested =
        "requested — the next attempt starts through the rework queue; a refusal is posted on the ticket";
    public const string NothingQueued = " Nothing was requested; ask again once they have landed.";

    public async Task<string> StartNextAsync(string reason, CancellationToken cancellationToken)
    {
        logger.LogInformation("Next attempt of {Project}/#{Ticket} asked by {Poster} in conversation {Conversation}: {Reason}",
            project.Name, ticketId, poster, conversation, reason);
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var key = TicketLandingRecorder.KeyOf(project, ticketId);
        var (_, unmet) = await services.GetRequiredService<UnmetTicketRequirements>().OfAsync(project.Tracker.Name, key, cancellationToken);
        if (unmet.Count > 0) return RequiresUnmetGate.Sentence(unmet) + NothingQueued;
        if (await services.GetRequiredService<PendingSeriesEditReader>().ReadAsync(project, ticketId, cancellationToken) is { } pending)
            return await services.GetRequiredService<PendingEditStart>()
                .StartAsync(project, ticketId, pending, conversation, poster, cancellationToken);
        var latest = await services.GetRequiredService<IPreviousAttemptReader>().LatestAsync(project.Name, ticketId, null, cancellationToken);
        if (latest is null) return $"Ticket {ticketId} has no attempt to continue; nothing was requested.";
        var result = await services.GetRequiredService<RunRetry>()
            .RetryAsync(latest.RunId, poster, RetryOrigin.Conversation, cancellationToken);
        return Say(result);
    }

    private static string Say(RunRetryResult result) => result.Kind switch
    {
        RunRetryResultKind.Requested => Requested,
        RunRetryResultKind.UnknownRun => "The latest attempt is no longer stored; nothing was requested.",
        _ => $"Not started: {result.Text}.",
    };
}
