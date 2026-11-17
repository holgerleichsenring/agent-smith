using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Turns;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog.RunMessages;

/// <summary>
/// 2026-11-17-4860b: the ticket conversations, as a running code attempt reads them — the operator turns the
/// conversation addressed to the run (<see cref="SpecDialogRunDivert"/>), from the stored transcript, so the run reads
/// them on whichever replica it runs. Singleton: each read opens its own scope.
/// </summary>
public sealed class TicketConversationMessages(IServiceScopeFactory scopes) : ITicketConversationMessages
{
    public async Task<DateTimeOffset?> LastActivityAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        if (Key(scope, project, ticketId) is not { } ticket) return null;
        var (tracker, key) = ticket;
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<SpecDialogSession>().AsNoTracking()
            .Where(s => s.Tracker == tracker && s.TicketKey == key)
            .OrderByDescending(s => s.Id)
            .Select(s => (DateTimeOffset?)s.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RunAddressedMessage>> AddressedToAsync(
        string project, string ticketId, string runId, int afterIndex, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        if (Key(scope, project, ticketId) is not { } ticket) return [];
        var (tracker, key) = ticket;
        var session = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>()
            .GetByTicketAsync(tracker, key, cancellationToken);
        if (session is null) return [];
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        return [.. transcript.Select((turn, index) => (turn, index))
            .Where(t => t.index > afterIndex && t.turn.Role == TranscriptRole.User
                && string.Equals(t.turn.RunId, runId, StringComparison.Ordinal))
            .Select(t => new RunAddressedMessage(t.index, t.turn.Text, t.turn.At))];
    }

    // The conversation of a ticket is keyed by its tracker connection and the ticket key the project spells.
    private static (string Tracker, string Key)? Key(IServiceScope scope, string project, string ticketId) =>
        scope.ServiceProvider.GetRequiredService<TicketProjects>().Of(project) is { } resolved
            ? (resolved.Tracker.Name, TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value)
            : null;
}
