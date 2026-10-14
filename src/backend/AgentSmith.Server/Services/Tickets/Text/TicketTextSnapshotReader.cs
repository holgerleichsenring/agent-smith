using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Conversation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>
/// The newest ticket text an attempt kept when it fetched the ticket — description and criteria as text, the title
/// from its run row — numbered as the detail numbers attempts. Null when no attempt kept one.
/// </summary>
public sealed class TicketTextSnapshotReader(IServiceScopeFactory scopes)
{
    /// <param name="attempts">The ticket's attempts, oldest first.</param>
    public async Task<TicketTextView?> ReadAsync(IReadOnlyList<Run> attempts, CancellationToken ct)
    {
        var runIds = attempts.Select(r => r.Id).ToList();
        using var scope = scopes.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunConversationEntry>().AsNoTracking()
            .Where(e => runIds.Contains(e.RunId) && e.Kind == RunConversationKinds.TicketText)
            .ToListAsync(ct);
        var newest = rows.OrderByDescending(e => e.At).ThenByDescending(e => e.Id).FirstOrDefault();
        if (newest is null) return null;
        var number = runIds.IndexOf(newest.RunId) + 1;
        return TicketTextView.FromSnapshot(attempts[number - 1].TicketTitle, newest.Text,
            ConversationRunEntries.Criteria(newest), newest.At, newest.RunId, number);
    }
}
