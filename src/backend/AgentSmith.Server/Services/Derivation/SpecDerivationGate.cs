using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Derivation;

/// <summary>
/// 2026-10-26-bdd0e: whether a code attempt has the ticket in flight without holding its lease — waiting for an
/// answer, or queued for capacity. Either will run again on its own, so a derivation now would race it.
/// </summary>
public sealed class SpecDerivationGate(IPreviousAttemptReader attempts)
{
    /// <summary>The sentence the request is refused with, or null when no attempt is in flight.</summary>
    public async Task<string?> RefusalAsync(string project, string ticketId, CancellationToken ct)
    {
        if (await attempts.HasQueuedAsync(project, ticketId, ct)) return SpecDerivationTexts.Queued;
        var latest = await attempts.LatestAsync(project, ticketId, null, ct);
        return latest?.Status == RunStatuses.WaitingForInput ? SpecDerivationTexts.Waiting : null;
    }
}
