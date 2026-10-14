using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-10-14-dbcem: shows on the tracker that one ticket requires another — a person's view of a requirement the
/// requirements table already holds, written once and never read back. Never throws except for the token's own
/// cancellation: a tracker that refuses or cannot be reached is a <see cref="RequiresLinkResult"/>.
/// </summary>
public interface ITicketRequiresLink
{
    Task<RequiresLinkResult> LinkAsync(TicketId dependent, TicketId required, CancellationToken cancellationToken);
}
