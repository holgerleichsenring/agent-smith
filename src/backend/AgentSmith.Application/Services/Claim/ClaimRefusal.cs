using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-09-21-77d6: the one seam that answers "would the claim refuse this request, and with
/// what result?" — the config pre-check first, then the standing-unmoved gate, in the order the
/// claim service has always asked them. The order is load-bearing: the gate reads
/// config.Projects by name, so an unknown project has to be refused before it is reached.
/// Both refusals are cheap and write nothing — a pure function of the request and the
/// configuration, and one store read.
/// <para>
/// It exists because a SECOND caller asks the same question: the spawn funnel, immediately
/// before it defers a ticket to the capacity queue. A ticket the claim would refuse must not be
/// queued — the pump claims the head, is refused, and drops the entry as permanent, so the next
/// poll finds no head and mints another waiting run. Two copies of "what the claim would refuse"
/// would drift; this is one call, and the claim path keeps its own reading of it.
/// </para>
/// </summary>
internal sealed class ClaimRefusal(IUnmovedTicketStore unmovedTickets)
{
    /// <summary>The refusal this request would be given, or null when nothing refuses it.</summary>
    public async Task<ClaimResult?> ForAsync(
        ClaimRequest request, AgentSmithConfig config, CancellationToken cancellationToken)
    {
        var rejection = ClaimPreChecker.Check(request, config);
        if (rejection is not null) return ClaimResult.Rejected(rejection.Value);

        // 2026-09-18-c1a7: the ticket the last run could not move — one store read, before the
        // lock and before any tracker write.
        return await new UnmovedTicketGate(unmovedTickets).RefusalAsync(request, config, cancellationToken);
    }
}
