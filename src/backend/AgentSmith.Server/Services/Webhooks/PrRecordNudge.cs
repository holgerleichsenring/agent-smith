using AgentSmith.Application.Services;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Asks for a ticket's pull-request record to be read again after a change that is no act. The
/// ticket is the owner of a same-repository ticket branch; a delivery without a head (a GitHub
/// top-level comment) finds it through the record its URL keys. No host call. The nudge is
/// Refresh-origin, so it starts, refuses and announces nothing; a webhook's is not late, a sweep's
/// is. A failure is logged and never keeps the delivery's act from its admission.
/// </summary>
public sealed class PrRecordNudge(
    IPrTicketOwner owners, IReworkNudges nudges, IServiceScopeFactory scopes, ILogger<PrRecordNudge> logger)
{
    public async Task<bool> NudgeAsync(PrRecordDelivery delivery, CancellationToken ct)
    {
        try
        {
            if (await TicketAsync(delivery, ct) is not { } ticket) return false;
            await nudges.EnqueueAsync(new ReworkNudgeRequest(ticket.Project, ticket.TicketId, ReworkNudgeOrigin.Refresh, ticket.Url)
            {
                FromDelivery = delivery.FromDelivery,
            }, ct);
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The record of pull request {Url} was not nudged", delivery.PrUrl);
            return false;
        }
    }

    private async Task<(string Project, string TicketId, string? Url)?> TicketAsync(PrRecordDelivery delivery, CancellationToken ct)
    {
        if (delivery.HeadRef is null) return await RecordAsync(delivery, ct);
        if (!delivery.SameRepository || TicketBranchNamer.TicketOf(delivery.HeadRef) is not { } ticketId) return null;
        return await owners.FindAsync(delivery.RepoUrl, ticketId, delivery.AllowedProjects, ct) is { } owner
            ? (owner.ProjectName, ticketId, delivery.PrUrl)
            : null;
    }

    private async Task<(string Project, string TicketId, string? Url)?> RecordAsync(PrRecordDelivery delivery, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(delivery.PrUrl)) return null;
        using var scope = scopes.CreateScope();
        var record = await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByPrKeyAsync(PrKey.Of(delivery.PrUrl), ct);
        return record is not null && (delivery.AllowedProjects is null || delivery.AllowedProjects.Contains(record.Project))
            ? (record.Project, record.TicketId, record.Url)
            : null;
    }
}
