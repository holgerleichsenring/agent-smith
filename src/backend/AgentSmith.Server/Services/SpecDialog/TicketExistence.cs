using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Text;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: reads each required ticket once on the project's tracker. A throw is classified by
/// <see cref="TicketReadFailureReason"/>, so Azure DevOps' TF401232 — which never reaches its provider's own
/// not-found — is missing like any tracker's 404, and an unreadable id carries its class, never the SDK's text.
/// </summary>
public sealed class TicketExistence(ITicketProviderFactory providers, ILogger<TicketExistence> logger) : ITicketExistence
{
    public async Task<TicketExistenceResult> CheckAsync(
        ResolvedProject project, IReadOnlyList<string> ids, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(ids);
        var distinct = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return TicketExistenceResult.None;
        var tracker = providers.Create(project.Tracker);
        var (missing, unreadable) = (new List<string>(), new List<UnreadableTicket>());
        foreach (var id in distinct)
        {
            try
            {
                await tracker.GetTicketAsync(new TicketId(id), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var reason = TicketReadFailureReason.Of(ex);
                logger.LogInformation("Required ticket {Ticket} in {Project} was not read: {Reason}", id, project.Name, reason);
                if (reason == TicketTextReasons.NotFound) missing.Add(id);
                else unreadable.Add(new UnreadableTicket(id, reason));
            }
        }
        return new TicketExistenceResult(missing, unreadable);
    }
}
