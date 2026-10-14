using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>2026-10-14-dbcem: the link of a factory that builds none — an answer naming why, never a throw.</summary>
public sealed class RefusingRequiresLink(string reason) : ITicketRequiresLink
{
    public Task<RequiresLinkResult> LinkAsync(TicketId dependent, TicketId required, CancellationToken cancellationToken) =>
        Task.FromResult(RequiresLinkResult.Refused(reason));
}
